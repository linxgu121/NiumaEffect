using System;
using System.Collections.Generic;
using NiumaEffect.Config;
using NiumaEffect.Controller;
using NiumaEffect.Data;
using NiumaEffect.Enum;
using NiumaEffect.ViewData;
using UnityEngine;

namespace NiumaEffect.Bridge
{
    /// <summary>
    /// 效果模块到 UI 模块的数据驱动桥接层。
    /// 桥接层按效果修订号拉取指定 Actor 的效果快照，不订阅事件，也不直接依赖具体 UI 框架。
    /// </summary>
    public sealed class EffectUIViewBridge : MonoBehaviour
    {
        [Header("模块引用")]
        [Tooltip("效果模块根控制器。请拖入场景中的 NiumaEffectController；为空时可按配置自动查找。")]
        [SerializeField] private NiumaEffectController effectController;

        [Tooltip("效果栏 UI 脚本。拖团队制作的 Buff/Debuff 图标栏脚本；该脚本负责显示效果图标、层数和剩余时间。当前模块未内置正式面板，未制作 UI 时可留空。")]
        [SerializeField] private MonoBehaviour effectUIReceiverProvider;

        [Header("自动查找")]
        [Tooltip("没有手动绑定效果控制器时，是否在场景中自动查找 NiumaEffectController。正式场景建议手动绑定。")]
        [SerializeField] private bool autoFindEffectController = true;

        [Header("刷新策略")]
        [Tooltip("启用桥接层时是否立即刷新一次效果面板。")]
        [SerializeField] private bool refreshOnEnable = true;

        [Tooltip("是否在 LateUpdate 中按效果版本号自动刷新 UI。关闭后需要外部手动调用 RefreshEffectPanel。")]
        [SerializeField] private bool refreshInLateUpdate = true;

        [Tooltip("没有效果服务、Actor 为空或没有可显示效果时，是否发送 Cleared 更新给 UI 接收接口。")]
        [SerializeField] private bool notifyWhenCleared = true;

        [Tooltip("倒计时效果的 UI 刷新间隔。效果剩余时间不会触发 Revision 递增，因此需要用该间隔刷新表现层；小于等于 0 表示只按 Revision 刷新。")]
        [SerializeField] private float countdownRefreshInterval = 0.2f;

        [Header("显示对象")]
        [Tooltip("当前显示的 ActorId。玩家可填 player；NPC、召唤物或远端玩家请填对应稳定 ActorId。")]
        [SerializeField] private string actorId = "player";

        [Header("筛选")]
        [Tooltip("只显示包含该标签的效果。为空表示不过滤标签。")]
        [SerializeField] private string requiredTag;

        [Tooltip("是否显示正面效果。")]
        [SerializeField] private bool includeBeneficial = true;

        [Tooltip("是否显示负面效果。")]
        [SerializeField] private bool includeHarmful = true;

        [Tooltip("是否显示中性效果。")]
        [SerializeField] private bool includeNeutral = true;

        [Tooltip("配置缺失的效果是否仍显示为未知效果。建议开启，避免旧存档或热更新配置缺失时 UI 静默消失。")]
        [SerializeField] private bool includeMissingDefinitions = true;

        [Header("日志")]
        [Tooltip("桥接层缺少必要引用、Receiver 类型错误或检测到 UI 刷新回流时是否打印警告。")]
        [SerializeField] private bool logWarnings = true;

        private readonly List<EffectIconViewData> _effectBuffer = new List<EffectIconViewData>();
        private IEffectUIReceiver _receiver;
        private long _observedRevision = -1L;
        private EffectPanelViewData _lastPanelData;
        private bool _hadPanelData;
        private bool _hasVisibleDurationEffect;
        private bool _isApplyingUpdate;
        private bool _refreshRequested;
        private float _nextCountdownRefreshTime;
        private long _lastBuildFailureRevision = long.MinValue;

        private void Reset()
        {
            ResolveReferences(false);
        }

        private void OnEnable()
        {
            ResolveReferences(true);
            _observedRevision = -1L;
            _isApplyingUpdate = false;

            if (refreshOnEnable)
            {
                RefreshEffectPanel();
            }
        }

        private void OnDisable()
        {
            _isApplyingUpdate = false;
            _refreshRequested = false;
            _hasVisibleDurationEffect = false;
        }

        private void LateUpdate()
        {
            if (_refreshRequested)
            {
                _refreshRequested = false;
                RefreshEffectPanel();
                return;
            }

            if (!refreshInLateUpdate || !EnsureController())
            {
                return;
            }

            if (_observedRevision == effectController.EffectRevision)
            {
                if (_hasVisibleDurationEffect && countdownRefreshInterval > 0f && Time.unscaledTime >= _nextCountdownRefreshTime)
                {
                    RefreshEffectPanel();
                }

                return;
            }

            RefreshEffectPanel();
        }

        /// <summary>
        /// 手动刷新效果面板。
        /// 只读取效果状态，不施加、移除或驱散任何效果。
        /// </summary>
        public void RefreshEffectPanel()
        {
            if (!EnsureController())
            {
                ApplyClearUpdate();
                return;
            }

            var targetRevision = effectController.EffectRevision;
            EffectPanelViewData panelData;
            try
            {
                panelData = BuildPanelViewData(targetRevision);
            }
            catch (Exception exception)
            {
                _observedRevision = -1L;
                if (logWarnings && _lastBuildFailureRevision != targetRevision)
                {
                    Debug.LogError($"[NiumaEffectUIBridge] 构建效果 UI 表现数据失败，桥接层会在下一次刷新时重试。Revision={targetRevision}, Error={exception.Message}", this);
                }

                _lastBuildFailureRevision = targetRevision;
                return;
            }

            _lastBuildFailureRevision = long.MinValue;
            _observedRevision = targetRevision;
            _hasVisibleDurationEffect = HasDurationEffect(panelData);
            ScheduleNextCountdownRefresh();
            if (panelData == null || panelData.Effects.Length == 0)
            {
                ApplyClearUpdate();
                return;
            }

            _hadPanelData = true;
            ApplyRawUpdate(new EffectUIUpdate(
                EffectUIUpdateType.Refresh,
                _observedRevision,
                panelData,
                _lastPanelData));
            _lastPanelData = panelData;
        }

        /// <summary>
        /// 设置当前显示 Actor 并请求下一帧刷新。
        /// </summary>
        public void SetActorId(string value)
        {
            if (!string.Equals(actorId, value, StringComparison.Ordinal))
            {
                _lastPanelData = null;
                _hadPanelData = false;
            }

            actorId = value;
            RequestRefresh();
        }

        /// <summary>
        /// 清空当前显示 Actor 并请求下一帧刷新。
        /// </summary>
        public void ClearActor()
        {
            SetActorId(null);
        }

        /// <summary>
        /// 设置标签筛选并请求下一帧刷新。
        /// </summary>
        public void SetRequiredTag(string value)
        {
            requiredTag = value;
            RequestRefresh();
        }

        private EffectPanelViewData BuildPanelViewData(long revision)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return null;
            }

            var effects = effectController.GetEffects(actorId);
            _effectBuffer.Clear();
            for (var i = 0; effects != null && i < effects.Length; i++)
            {
                var snapshot = effects[i];
                if (!ShouldShowEffect(snapshot))
                {
                    continue;
                }

                _effectBuffer.Add(BuildIconViewData(snapshot));
            }

            return new EffectPanelViewData
            {
                ActorId = actorId,
                Revision = revision,
                Effects = _effectBuffer.ToArray()
            };
        }

        private EffectIconViewData BuildIconViewData(ActiveEffectSnapshot snapshot)
        {
            var definition = FindDefinition(snapshot.EffectId);
            var isMissing = snapshot.IsMissingDefinition || definition == null;
            var duration = Math.Max(0f, snapshot.DurationSeconds);
            var remaining = Math.Max(0f, snapshot.RemainingSeconds);

            return new EffectIconViewData
            {
                EffectInstanceId = snapshot.EffectInstanceId,
                EffectId = snapshot.EffectId,
                DisplayName = ResolveDisplayName(snapshot, definition, isMissing),
                Description = isMissing ? string.Empty : definition.Description,
                IconAddress = isMissing ? string.Empty : definition.IconAddress,
                Type = isMissing ? snapshot.Type : definition.Type,
                Polarity = isMissing ? snapshot.Polarity : definition.Polarity,
                StackCount = Math.Max(1, snapshot.StackCount),
                DurationSeconds = duration,
                RemainingSeconds = remaining,
                Progress01 = duration > 0f ? Mathf.Clamp01(remaining / duration) : 1f,
                IsPermanent = duration <= 0f,
                IsDispellable = snapshot.IsDispellable,
                IsMissingDefinition = isMissing,
                Tags = CopyTags(snapshot.Tags, definition)
            };
        }

        private bool ShouldShowEffect(ActiveEffectSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.EffectInstanceId))
            {
                return false;
            }

            if (!includeMissingDefinitions && snapshot.IsMissingDefinition)
            {
                return false;
            }

            if (!ShouldShowPolarity(snapshot.Polarity))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(requiredTag) && !ContainsTag(snapshot.Tags, requiredTag))
            {
                return false;
            }

            return true;
        }

        private bool ShouldShowPolarity(EffectPolarity polarity)
        {
            switch (polarity)
            {
                case EffectPolarity.Beneficial:
                    return includeBeneficial;
                case EffectPolarity.Harmful:
                    return includeHarmful;
                case EffectPolarity.Neutral:
                    return includeNeutral;
                default:
                    return includeNeutral;
            }
        }

        private EffectDefinition FindDefinition(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId) || effectController == null)
            {
                return null;
            }

            var definitions = effectController.EffectDefinitions;
            for (var i = 0; definitions != null && i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition != null && string.Equals(definition.EffectId, effectId, StringComparison.Ordinal))
                {
                    return definition;
                }
            }

            return null;
        }

        private void ApplyClearUpdate()
        {
            _observedRevision = effectController != null ? effectController.EffectRevision : -1L;
            _hasVisibleDurationEffect = false;
            if (!notifyWhenCleared && !_hadPanelData)
            {
                return;
            }

            ApplyRawUpdate(new EffectUIUpdate(
                EffectUIUpdateType.Cleared,
                _observedRevision,
                null,
                _lastPanelData));
            _lastPanelData = null;
            _hadPanelData = false;
        }

        private void ApplyRawUpdate(EffectUIUpdate update)
        {
            _receiver = ResolveReceiver(true);
            if (_receiver == null)
            {
                return;
            }

            if (_isApplyingUpdate)
            {
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaEffectUIBridge] 检测到 UI 刷新回流，本次刷新已延后到下一帧。", this);
                }

                return;
            }

            var revisionBeforeApply = effectController != null ? effectController.EffectRevision : update.Revision;
            try
            {
                _isApplyingUpdate = true;
                _receiver.ApplyEffectUpdate(update);
            }
            finally
            {
                _isApplyingUpdate = false;
            }

            if (effectController != null && effectController.EffectRevision != revisionBeforeApply)
            {
                _observedRevision = -1L;
                _refreshRequested = true;
                if (logWarnings)
                {
                    Debug.LogWarning("[NiumaEffectUIBridge] UI 接收器回调中修改了效果数据，桥接层会在下一帧重新刷新。请把效果命令放到输入、交互或业务管线中处理。", this);
                }
            }
        }

        private void RequestRefresh()
        {
            _observedRevision = -1L;
            _refreshRequested = true;
        }

        private void ScheduleNextCountdownRefresh()
        {
            if (_hasVisibleDurationEffect && countdownRefreshInterval > 0f)
            {
                _nextCountdownRefreshTime = Time.unscaledTime + countdownRefreshInterval;
            }
        }

        private bool EnsureController()
        {
            ResolveEffectController(true);
            return effectController != null;
        }

        private void ResolveReferences(bool logMissing)
        {
            ResolveEffectController(logMissing);
            _receiver = ResolveReceiver(logMissing);
        }

        private void ResolveEffectController(bool logMissing)
        {
            if (effectController != null)
            {
                return;
            }

            if (autoFindEffectController)
            {
#if UNITY_2023_1_OR_NEWER
                effectController = FindFirstObjectByType<NiumaEffectController>();
#else
                effectController = FindObjectOfType<NiumaEffectController>();
#endif
            }

            if (effectController == null && logWarnings && logMissing)
            {
                Debug.LogWarning("[NiumaEffectUIBridge] 未找到 NiumaEffectController，请在 Inspector 中绑定效果控制器。", this);
            }
        }

        private IEffectUIReceiver ResolveReceiver(bool logMissing)
        {
            var receiver = effectUIReceiverProvider as IEffectUIReceiver;
            if (receiver == null && logWarnings && logMissing && effectUIReceiverProvider != null)
            {
                Debug.LogWarning("[NiumaEffectUIBridge] Effect UI Receiver 绑定的不是效果栏脚本，请拖团队制作的 Buff/Debuff 图标栏脚本。", this);
            }

            return receiver;
        }

        private static string ResolveDisplayName(ActiveEffectSnapshot snapshot, EffectDefinition definition, bool isMissing)
        {
            if (isMissing)
            {
                return string.IsNullOrWhiteSpace(snapshot?.EffectId) ? "未知效果" : $"未知效果({snapshot.EffectId})";
            }

            return !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : definition.EffectId;
        }

        private static string[] CopyTags(string[] snapshotTags, EffectDefinition definition)
        {
            if (snapshotTags != null && snapshotTags.Length > 0)
            {
                var result = new string[snapshotTags.Length];
                Array.Copy(snapshotTags, result, snapshotTags.Length);
                return result;
            }

            var definitionTags = definition != null ? definition.Tags : null;
            if (definitionTags == null || definitionTags.Length == 0)
            {
                return Array.Empty<string>();
            }

            var fallback = new string[definitionTags.Length];
            Array.Copy(definitionTags, fallback, definitionTags.Length);
            return fallback;
        }

        private static bool ContainsTag(string[] tags, string tag)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            for (var i = 0; i < tags.Length; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDurationEffect(EffectPanelViewData panelData)
        {
            var effects = panelData != null ? panelData.Effects : null;
            for (var i = 0; effects != null && i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect != null && !effect.IsPermanent && effect.DurationSeconds > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
