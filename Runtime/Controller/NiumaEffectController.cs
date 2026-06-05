using System;
using NiumaAttribute.Controller;
using NiumaAttribute.Service;
using NiumaCore.Event;
using NiumaCore.Module;
using NiumaEffect.Config;
using NiumaEffect.Data;
using NiumaEffect.Enum;
using NiumaEffect.Request;
using NiumaEffect.Result;
using NiumaEffect.Service;
using UnityEngine;

namespace NiumaEffect.Controller
{
    /// <summary>
    /// NiumaEffect 效果模块根控制器。
    /// 负责把纯 C# 的 EffectService 接入 Unity 生命周期、Inspector 配置、GameContext 和基础调试入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NiumaEffectController : MonoBehaviour, IGameModule
    {
        [Header("效果配置")]
        [Tooltip("效果定义列表。请拖入当前版本可用的 EffectDefinition，EffectId 必须稳定。")]
        [SerializeField] private EffectDefinition[] effectDefinitions = Array.Empty<EffectDefinition>();

        [Tooltip("效果标签公共清单。用于配置校验和团队命名约束，可为空。")]
        [SerializeField] private EffectTagCatalog effectTagCatalog;

        [Header("属性依赖")]
        [Tooltip("属性模块控制器。没有统一 GameContext 时可手动绑定；效果模块会从这里读取 IAttributeCommand。")]
        [SerializeField] private NiumaAttributeController attributeController;

        [Tooltip("初始化时是否尝试从 GameContext 解析 IAttributeCommand。使用统一模块启动器时建议开启。")]
        [SerializeField] private bool resolveAttributeFromContext = true;

        [Header("外部裁决")]
        [Tooltip("效果施加规则脚本。需要技能、道具或剧情决定能否施加效果时，拖对应的 EffectApplicationResolver；第一版没有额外规则时可留空。")]
        [SerializeField] private MonoBehaviour applicationResolverBehaviour;

        [Tooltip("初始化时是否尝试从 GameContext 解析 IEffectApplicationResolver。使用统一模块启动器时建议开启。")]
        [SerializeField] private bool resolveApplicationResolverFromContext = true;

        [Tooltip("效果免疫规则脚本。需要免疫、抗性或不可驱散规则时，拖对应的 EffectImmunityResolver；第一版没有免疫规则时可留空。")]
        [SerializeField] private MonoBehaviour immunityResolverBehaviour;

        [Tooltip("初始化时是否尝试从 GameContext 解析 IEffectImmunityResolver。使用统一模块启动器时建议开启。")]
        [SerializeField] private bool resolveImmunityResolverFromContext = true;

        [Header("生命周期事件")]
        [Tooltip("是否发布效果生命周期事件。事件只做通知，不作为核心一致性的依赖。")]
        [SerializeField] private bool publishLifecycleEvents;

        [Tooltip("效果生命周期事件派发信道。Immediate 立即派发，Deferred 由 GameContext 统一 Drain。")]
        [SerializeField] private EventChannel lifecycleEventChannel = EventChannel.Immediate;

        [Header("模块启动")]
        [Tooltip("Awake 时是否自动初始化效果服务。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool initializeOnAwake = true;

        [Tooltip("OnEnable 时是否自动启动效果模块。没有统一模块启动器时建议开启。")]
        [SerializeField] private bool startOnEnable = true;

        [Tooltip("初始化时是否把 IEffectService、IEffectQuery、IEffectCommand 注册到 GameContext。使用统一 GameContext 的项目建议开启。")]
        [SerializeField] private bool registerServiceToContext = true;

        [Tooltip("是否由本控制器的 Update 自动驱动 Tick。若项目已有统一模块启动器调用 IGameModule.Tick，请关闭，避免效果过期每帧执行两次。")]
        [SerializeField] private bool driveTickInUpdate = true;

        [Header("调试：效果请求")]
        [Tooltip("调试用效果 ID。右键组件菜单会用它添加效果。")]
        [SerializeField] private string debugEffectId;

        [Tooltip("调试用目标 ActorId。")]
        [SerializeField] private string debugOwnerActorId = "player";

        [Tooltip("调试用来源 ActorId。可以为空。")]
        [SerializeField] private string debugSourceActorId;

        [Tooltip("调试来源模块名。")]
        [SerializeField] private string debugSourceModule = "debug";

        [Tooltip("调试施加层数。小于 1 时服务层按 1 处理。")]
        [SerializeField] private int debugStackCount = 1;

        [Tooltip("调试持续时间覆盖值。小于等于 0 时使用配置持续时间。")]
        [SerializeField] private float debugDurationOverrideSeconds = -1f;

        [Tooltip("调试用效果实例 ID。右键菜单移除效果时使用。")]
        [SerializeField] private string debugEffectInstanceId;

        [Tooltip("调试驱散时使用的标签。为空表示不按标签过滤。")]
        [SerializeField] private string debugDispelTag;

        [Tooltip("调试驱散的最大数量。小于等于 0 表示不限制。")]
        [SerializeField] private int debugDispelMaxCount;

        private IEffectService _effectService;
        private IEffectConfigurationService _configurationService;
        private GameContext _context;
        private IAttributeCommand _attributeCommand;
        private IEffectApplicationResolver _applicationResolver;
        private IEffectImmunityResolver _immunityResolver;
        private bool _attributeCommandLocked;
        private bool _applicationResolverLocked;
        private bool _immunityResolverLocked;
        private bool _warnedMissingDefinitions;
        private bool _warnedMissingAttributeCommand;
        private bool _warnedInvalidApplicationResolver;
        private bool _warnedInvalidImmunityResolver;
        private bool _warnedInitializeFailure;
        private bool _warnedServiceNotReady;
        private bool _autoInitializeFailed;
        private bool _isDestroyed;

        /// <summary>模块名称。</summary>
        public string ModuleName => "NiumaEffect";

        /// <summary>效果服务门面接口。</summary>
        public IEffectService EffectService => _effectService;

        /// <summary>效果查询接口。</summary>
        public IEffectQuery EffectQuery => _effectService;

        /// <summary>效果命令接口。</summary>
        public IEffectCommand EffectCommand => _effectService;

        /// <summary>当前模块是否已经初始化。</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>当前模块是否正在运行。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>效果模块全局修订号。</summary>
        public long EffectRevision => _effectService != null ? _effectService.Revision : 0L;

        /// <summary>当前效果定义配置。</summary>
        public EffectDefinition[] EffectDefinitions => effectDefinitions ?? Array.Empty<EffectDefinition>();

        /// <summary>最近一次施加效果结果。</summary>
        public EffectApplyResult LastApplyResult { get; private set; }

        /// <summary>最近一次效果操作结果。</summary>
        public EffectOperationResult LastOperationResult { get; private set; }

        private void Awake()
        {
            if (initializeOnAwake && !IsInitialized)
            {
                Initialize(null);
            }
        }

        private void OnEnable()
        {
            if (startOnEnable && IsInitialized && !IsRunning)
            {
                StartModule();
            }
        }

        private void OnDisable()
        {
            if (IsRunning)
            {
                StopModule();
            }
        }

        private void OnDestroy()
        {
            UnregisterServicesFromContext();
            IsRunning = false;
            IsInitialized = false;
            _isDestroyed = true;
            _effectService = null;
            _configurationService = null;
        }

        /// <summary>
        /// 初始化效果模块。
        /// 如果已有服务，会导出效果快照并在新服务中恢复，避免重复初始化丢失运行中效果。
        /// </summary>
        public void Initialize(GameContext context)
        {
            var wasRunning = IsRunning;
            var previousService = _effectService;
            var previousConfigurationService = _configurationService;
            var previousContext = _context;
            var previousAttributeCommand = _attributeCommand;
            var previousApplicationResolver = _applicationResolver;
            var previousImmunityResolver = _immunityResolver;
            var previousInitialized = IsInitialized;
            var targetContext = context ?? _context;
            var previousRegisteredService = targetContext != null ? targetContext.GetService<IEffectService>() : null;
            var previousRegisteredQuery = targetContext != null ? targetContext.GetService<IEffectQuery>() : null;
            var previousRegisteredCommand = targetContext != null ? targetContext.GetService<IEffectCommand>() : null;
            var initializedSuccessfully = false;
            EffectService newService = null;
            IsRunning = false;

            try
            {
                _context = targetContext;
                WarnIfConfigMissing();

                if (!_attributeCommandLocked)
                {
                    _attributeCommand = ResolveAttributeCommand(_context);
                }

                if (!_applicationResolverLocked)
                {
                    _applicationResolver = ResolveApplicationResolver(_context);
                }

                if (!_immunityResolverLocked)
                {
                    _immunityResolver = ResolveImmunityResolver(_context);
                }

                WarnIfAttributeCommandMissing();

                var snapshot = previousService != null ? previousService.ExportSnapshot() : null;
                newService = new EffectService(effectDefinitions, _attributeCommand, _applicationResolver, _immunityResolver, _context?.EventBus);
                newService.SetTagCatalog(effectTagCatalog);
                newService.SetEventBus(_context?.EventBus, lifecycleEventChannel, publishLifecycleEvents);
                if (snapshot != null)
                {
                    LastOperationResult = newService.ImportSnapshot(snapshot);
                }

                _effectService = newService;
                _configurationService = newService;
                RegisterServicesToContext();
                IsInitialized = true;
                _warnedInitializeFailure = false;
                _autoInitializeFailed = false;
                _warnedServiceNotReady = false;
                initializedSuccessfully = true;
            }
            catch (Exception exception)
            {
                if (!_warnedInitializeFailure)
                {
                    Debug.LogError($"[NiumaEffect] 初始化效果模块失败：{exception.Message}", this);
                    _warnedInitializeFailure = true;
                }

                RestoreRegisteredEffectServices(targetContext, previousRegisteredService, previousRegisteredQuery, previousRegisteredCommand, newService);
                _effectService = previousService;
                _configurationService = previousConfigurationService;
                _context = previousContext;
                _attributeCommand = previousAttributeCommand;
                _applicationResolver = previousApplicationResolver;
                _immunityResolver = previousImmunityResolver;
                IsInitialized = previousInitialized;
            }
            finally
            {
                IsRunning = initializedSuccessfully
                    ? wasRunning && _effectService != null
                    : wasRunning && previousInitialized && previousService != null;
            }
        }

        /// <summary>启动效果模块。</summary>
        public void StartModule()
        {
            if (!IsInitialized)
            {
                Initialize(_context);
            }

            IsRunning = _effectService != null;
        }

        /// <summary>
        /// 停止效果模块。
        /// 这里只关闭 Tick 驱动，不清空运行中效果，也不导出存档。
        /// </summary>
        public void StopModule()
        {
            IsRunning = false;
        }

        /// <summary>
        /// 模块帧更新。
        /// 驱动效果持续时间和过期清理。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsRunning || _effectService == null)
            {
                return;
            }

            _effectService.Tick(deltaTime);
        }

        private void Update()
        {
            if (!driveTickInUpdate)
            {
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>运行时替换效果定义。</summary>
        public void SetEffectDefinitions(EffectDefinition[] definitions)
        {
            effectDefinitions = definitions ?? Array.Empty<EffectDefinition>();
            _autoInitializeFailed = false;
            _warnedMissingDefinitions = false;
            _configurationService?.SetDefinitions(effectDefinitions);
        }

        /// <summary>运行时替换效果标签清单。</summary>
        public void SetEffectTagCatalog(EffectTagCatalog tagCatalog)
        {
            effectTagCatalog = tagCatalog;
            _configurationService?.SetTagCatalog(effectTagCatalog);
        }

        /// <summary>
        /// 运行时设置属性命令接口。
        /// 通常由统一模块启动器调用；调用后会锁定自动解析，避免后续 Initialize 静默覆盖。
        /// </summary>
        public void SetAttributeCommand(IAttributeCommand attributeCommand)
        {
            _attributeCommand = attributeCommand;
            _attributeCommandLocked = true;
            _autoInitializeFailed = false;
            _warnedMissingAttributeCommand = false;
            TryApplyExternalDependency(() => _configurationService?.SetAttributeCommand(_attributeCommand), "设置属性命令接口");
        }

        /// <summary>解除属性命令接口手动注入锁定，并重新从 Inspector 或 GameContext 解析。</summary>
        public void UnlockAttributeCommand()
        {
            _attributeCommandLocked = false;
            _attributeCommand = ResolveAttributeCommand(_context);
            _autoInitializeFailed = false;
            TryApplyExternalDependency(() => _configurationService?.SetAttributeCommand(_attributeCommand), "解除属性命令接口锁定");
        }

        /// <summary>运行时设置外部施加条件解析器。</summary>
        public void SetApplicationResolver(IEffectApplicationResolver resolver)
        {
            _applicationResolver = resolver;
            _applicationResolverLocked = true;
            _autoInitializeFailed = false;
            TryApplyExternalDependency(() => _configurationService?.SetApplicationResolver(_applicationResolver), "设置施加条件解析器");
        }

        /// <summary>解除外部施加条件解析器锁定，并重新解析。</summary>
        public void UnlockApplicationResolver()
        {
            _applicationResolverLocked = false;
            _applicationResolver = ResolveApplicationResolver(_context);
            _autoInitializeFailed = false;
            TryApplyExternalDependency(() => _configurationService?.SetApplicationResolver(_applicationResolver), "解除施加条件解析器锁定");
        }

        /// <summary>运行时设置免疫解析器。</summary>
        public void SetImmunityResolver(IEffectImmunityResolver resolver)
        {
            _immunityResolver = resolver;
            _immunityResolverLocked = true;
            _autoInitializeFailed = false;
            TryApplyExternalDependency(() => _configurationService?.SetImmunityResolver(_immunityResolver), "设置免疫解析器");
        }

        /// <summary>解除免疫解析器锁定，并重新解析。</summary>
        public void UnlockImmunityResolver()
        {
            _immunityResolverLocked = false;
            _immunityResolver = ResolveImmunityResolver(_context);
            _autoInitializeFailed = false;
            TryApplyExternalDependency(() => _configurationService?.SetImmunityResolver(_immunityResolver), "解除免疫解析器锁定");
        }

        /// <summary>运行时设置事件总线。</summary>
        public void SetEventBus(IEventBus eventBus, EventChannel channel, bool publishEvents)
        {
            lifecycleEventChannel = channel;
            publishLifecycleEvents = publishEvents;
            TryApplyExternalDependency(() => _configurationService?.SetEventBus(eventBus, lifecycleEventChannel, publishLifecycleEvents), "设置事件总线");
        }

        /// <summary>导出效果快照。</summary>
        public EffectSaveData ExportSnapshot()
        {
            return EnsureServiceReady(false) ? _effectService.ExportSnapshot() : new EffectSaveData();
        }

        /// <summary>导入效果快照。</summary>
        public EffectOperationResult ImportSnapshot(EffectSaveData snapshot)
        {
            if (snapshot == null)
            {
                LastOperationResult = EffectOperationResult.Failed(EffectOperationFailureReason.ImportInvalid, null, "导入快照为空。");
                return LastOperationResult;
            }

            if (!EnsureServiceReady(true))
            {
                LastOperationResult = EffectOperationResult.Failed(EffectOperationFailureReason.ServiceNotReady, null, "效果服务尚未准备好。");
                return LastOperationResult;
            }

            LastOperationResult = _effectService.ImportSnapshot(snapshot);
            return LastOperationResult;
        }

        public bool HasEffect(string actorId, string effectId)
        {
            return EnsureServiceReady(false) && _effectService.HasEffect(actorId, effectId);
        }

        public bool HasEffectWithTag(string actorId, string tag)
        {
            return EnsureServiceReady(false) && _effectService.HasEffectWithTag(actorId, tag);
        }

        public ActiveEffectSnapshot[] GetEffects(string actorId)
        {
            return EnsureServiceReady(false) ? _effectService.GetEffects(actorId) : Array.Empty<ActiveEffectSnapshot>();
        }

        public bool TryGetEffect(string actorId, string effectInstanceId, out ActiveEffectSnapshot snapshot)
        {
            snapshot = null;
            return EnsureServiceReady(false) && _effectService.TryGetEffect(actorId, effectInstanceId, out snapshot);
        }

        public EffectApplyResult ApplyEffect(EffectApplyRequest request)
        {
            if (!EnsureServiceReady(true))
            {
                LastApplyResult = EffectApplyResult.Failed(EffectApplyFailureReason.InvalidRequest, request?.OwnerActorId, request?.EffectId, "效果服务未初始化。");
                return LastApplyResult;
            }

            LastApplyResult = _effectService.ApplyEffect(request);
            return LastApplyResult;
        }

        public EffectOperationResult RemoveEffect(string actorId, string effectInstanceId, EffectRemoveReason reason = EffectRemoveReason.Manual)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = EffectOperationResult.Failed(EffectOperationFailureReason.ServiceNotReady, actorId, "效果服务未初始化。");
                return LastOperationResult;
            }

            LastOperationResult = _effectService.RemoveEffect(actorId, effectInstanceId, reason);
            return LastOperationResult;
        }

        public EffectOperationResult DispelEffects(EffectDispelRequest request)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = EffectOperationResult.Failed(EffectOperationFailureReason.ServiceNotReady, request?.ActorId, "效果服务未初始化。");
                return LastOperationResult;
            }

            LastOperationResult = _effectService.DispelEffects(request);
            return LastOperationResult;
        }

        public EffectOperationResult ClearEffects(string actorId, EffectRemoveReason reason = EffectRemoveReason.Manual)
        {
            if (!EnsureServiceReady(true))
            {
                LastOperationResult = EffectOperationResult.Failed(EffectOperationFailureReason.ServiceNotReady, actorId, "效果服务未初始化。");
                return LastOperationResult;
            }

            LastOperationResult = _effectService.ClearEffects(actorId, reason);
            return LastOperationResult;
        }

        [ContextMenu("NiumaEffect/重新初始化服务")]
        private void DebugReinitialize()
        {
            Initialize(_context);
            Debug.Log($"[NiumaEffect] 重新初始化完成：Initialized={IsInitialized}, Revision={EffectRevision}", this);
        }

        [ContextMenu("NiumaEffect/解除属性命令锁定并重新解析")]
        private void DebugUnlockAttributeCommand()
        {
            UnlockAttributeCommand();
            Debug.Log($"[NiumaEffect] 已重新解析属性命令接口：{_attributeCommand != null}", this);
        }

        [ContextMenu("NiumaEffect/添加调试效果")]
        private void DebugApplyEffect()
        {
            var result = ApplyEffect(CreateDebugApplyRequest());
            Debug.Log($"[NiumaEffect] 添加效果：Succeeded={result.Succeeded}, Reason={result.FailureReason}, EffectId={result.EffectId}, Instance={result.EffectInstanceId}, Message={result.Message}, Revision={EffectRevision}", this);
        }

        [ContextMenu("NiumaEffect/移除调试效果实例")]
        private void DebugRemoveEffect()
        {
            LogOperation("移除效果", RemoveEffect(debugOwnerActorId, debugEffectInstanceId, EffectRemoveReason.Manual));
        }

        [ContextMenu("NiumaEffect/驱散调试目标负面效果")]
        private void DebugDispelHarmful()
        {
            var tags = string.IsNullOrWhiteSpace(debugDispelTag)
                ? Array.Empty<string>()
                : new[] { debugDispelTag };
            var request = new EffectDispelRequest
            {
                ActorId = debugOwnerActorId,
                TargetPolarity = EffectPolarity.Harmful,
                RequiredTags = tags,
                MaxCount = debugDispelMaxCount,
                SourceActorId = debugSourceActorId,
                SourceModule = debugSourceModule
            };
            LogOperation("驱散负面效果", DispelEffects(request));
        }

        [ContextMenu("NiumaEffect/清空调试目标全部效果")]
        private void DebugClearEffects()
        {
            LogOperation("清空效果", ClearEffects(debugOwnerActorId, EffectRemoveReason.Manual));
        }

        [ContextMenu("NiumaEffect/打印调试目标效果")]
        private void DebugPrintEffects()
        {
            var effects = GetEffects(debugOwnerActorId);
            Debug.Log($"[NiumaEffect] Actor={debugOwnerActorId}, Effects={effects.Length}, Revision={EffectRevision}", this);
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                Debug.Log($"[NiumaEffect] #{i} EffectId={effect.EffectId}, Instance={effect.EffectInstanceId}, Stack={effect.StackCount}, Remaining={effect.RemainingSeconds}, Missing={effect.IsMissingDefinition}", this);
            }
        }

        private EffectApplyRequest CreateDebugApplyRequest()
        {
            return new EffectApplyRequest
            {
                EffectId = debugEffectId,
                OwnerActorId = debugOwnerActorId,
                SourceActorId = debugSourceActorId,
                SourceModule = debugSourceModule,
                StackCount = debugStackCount,
                DurationOverrideSeconds = debugDurationOverrideSeconds
            };
        }

        private bool EnsureServiceReady(bool allowAutoInitialize)
        {
            if (_effectService != null)
            {
                return true;
            }

            if (_isDestroyed || !allowAutoInitialize || _autoInitializeFailed)
            {
                WarnServiceNotReadyOnce();
                return false;
            }

            Initialize(_context);
            if (_effectService != null)
            {
                return true;
            }

            _autoInitializeFailed = true;
            WarnServiceNotReadyOnce();
            return false;
        }

        private IAttributeCommand ResolveAttributeCommand(GameContext context)
        {
            if (attributeController != null && attributeController.AttributeCommand != null)
            {
                return attributeController.AttributeCommand;
            }

            if (resolveAttributeFromContext && context != null && context.TryGetService<IAttributeCommand>(out var contextCommand))
            {
                return contextCommand;
            }

            return null;
        }

        private IEffectApplicationResolver ResolveApplicationResolver(GameContext context)
        {
            if (applicationResolverBehaviour != null)
            {
                if (applicationResolverBehaviour is IEffectApplicationResolver resolver)
                {
                    return resolver;
                }

                if (!_warnedInvalidApplicationResolver)
                {
                    Debug.LogWarning("[NiumaEffect] ApplicationResolver 绑定的不是效果施加规则脚本；没有额外施加规则时可留空。", this);
                    _warnedInvalidApplicationResolver = true;
                }
            }

            return resolveApplicationResolverFromContext
                   && context != null
                   && context.TryGetService<IEffectApplicationResolver>(out var contextResolver)
                ? contextResolver
                : null;
        }

        private IEffectImmunityResolver ResolveImmunityResolver(GameContext context)
        {
            if (immunityResolverBehaviour != null)
            {
                if (immunityResolverBehaviour is IEffectImmunityResolver resolver)
                {
                    return resolver;
                }

                if (!_warnedInvalidImmunityResolver)
                {
                    Debug.LogWarning("[NiumaEffect] ImmunityResolver 绑定的不是效果免疫规则脚本；没有免疫规则时可留空。", this);
                    _warnedInvalidImmunityResolver = true;
                }
            }

            return resolveImmunityResolverFromContext
                   && context != null
                   && context.TryGetService<IEffectImmunityResolver>(out var contextResolver)
                ? contextResolver
                : null;
        }

        private void RegisterServicesToContext()
        {
            if (_context == null || !registerServiceToContext)
            {
                return;
            }

            if (_effectService == null)
            {
                Debug.LogWarning("[NiumaEffect] 效果服务为空，已跳过 GameContext 注册，避免清除其它启动器注册的服务。", this);
                return;
            }

            _context.RegisterService<IEffectService>(_effectService);
            _context.RegisterService<IEffectQuery>(_effectService);
            _context.RegisterService<IEffectCommand>(_effectService);
        }

        private void UnregisterServicesFromContext()
        {
            if (_context == null || !registerServiceToContext || _effectService == null)
            {
                return;
            }

            ClearRegisteredServiceIfCurrent<IEffectService>(_context, _effectService);
            ClearRegisteredServiceIfCurrent<IEffectQuery>(_context, _effectService);
            ClearRegisteredServiceIfCurrent<IEffectCommand>(_context, _effectService);
        }

        private void RestoreRegisteredEffectServices(
            GameContext context,
            IEffectService service,
            IEffectQuery query,
            IEffectCommand command,
            EffectService failedService)
        {
            if (context == null || !registerServiceToContext)
            {
                return;
            }

            RestoreRegisteredService(context, service, failedService as IEffectService);
            RestoreRegisteredService(context, query, failedService as IEffectQuery);
            RestoreRegisteredService(context, command, failedService as IEffectCommand);
        }

        private static void RestoreRegisteredService<T>(GameContext context, T previousService, T failedService)
            where T : class
        {
            if (context == null)
            {
                return;
            }

            if (previousService != null)
            {
                context.RegisterService(previousService);
                return;
            }

            var currentService = context.GetService<T>();
            if (currentService == null || ReferenceEquals(currentService, failedService))
            {
                context.UnregisterService<T>();
            }
        }

        private static void ClearRegisteredServiceIfCurrent<T>(GameContext context, T service)
            where T : class
        {
            if (context == null || service == null)
            {
                return;
            }

            var currentService = context.GetService<T>();
            if (ReferenceEquals(currentService, service))
            {
                context.UnregisterService<T>();
            }
        }

        private void TryApplyExternalDependency(Action apply, string actionName)
        {
            if (apply == null)
            {
                return;
            }

            try
            {
                apply();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NiumaEffect] {actionName}失败：{exception.Message}", this);
            }
        }

        private void WarnServiceNotReadyOnce()
        {
            if (_warnedServiceNotReady)
            {
                return;
            }

            Debug.LogWarning("[NiumaEffect] 效果服务未就绪；只读查询不会自动初始化，请先由模块启动器显式调用 Initialize。", this);
            _warnedServiceNotReady = true;
        }

        private void WarnIfConfigMissing()
        {
            if ((effectDefinitions == null || effectDefinitions.Length == 0) && !_warnedMissingDefinitions)
            {
                Debug.LogWarning("[NiumaEffect] 未配置任何 EffectDefinition。效果服务可以创建，但无法添加正式效果。", this);
                _warnedMissingDefinitions = true;
            }
        }

        private void WarnIfAttributeCommandMissing()
        {
            if (_attributeCommand != null || _warnedMissingAttributeCommand)
            {
                return;
            }

            Debug.LogWarning("[NiumaEffect] 未解析到 IAttributeCommand。无 Modifier 的效果可以运行，需要属性加成的效果会施加失败。请绑定 attributeController 或通过 GameContext 注册 IAttributeCommand。", this);
            _warnedMissingAttributeCommand = true;
        }

        private void LogOperation(string actionName, EffectOperationResult result)
        {
            Debug.Log($"[NiumaEffect] {actionName}：Succeeded={result?.Succeeded}, Reason={result?.FailureReason}, Changed={result?.ChangedEffects?.Length ?? 0}, Message={result?.Message}, Revision={EffectRevision}", this);
        }
    }
}
