using System;
using System.Collections.Generic;
using NiumaAttribute.Service;
using NiumaCore.Event;
using NiumaEffect.Config;
using NiumaEffect.Data;
using NiumaEffect.Enum;
using NiumaEffect.Event;
using NiumaEffect.Request;
using NiumaEffect.Result;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 效果核心服务。
    /// 负责效果实例生命周期、叠层、驱散、快照导入导出，以及与 NiumaAttribute 的 Modifier 桥接。
    /// </summary>
    public sealed class EffectService : IEffectService, IEffectConfigurationService
    {
        private const string EffectSourceModule = "NiumaEffect";

        private readonly Dictionary<string, EffectDefinition> _definitions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ActiveEffectRuntimeState>> _effectsByActor = new(StringComparer.Ordinal);
        private readonly List<ActiveEffectRuntimeState> _expiredBuffer = new();
        private readonly List<ActiveEffectSnapshot> _changedSnapshotBuffer = new();

        private EffectTagCatalog _tagCatalog;
        private IAttributeCommand _attributeCommand;
        private IEffectApplicationResolver _applicationResolver;
        private IEffectImmunityResolver _immunityResolver;
        private IEventBus _eventBus;
        private EventChannel _lifecycleEventChannel = EventChannel.Immediate;
        private bool _publishLifecycleEvents;
        private long _revision;

        /// <inheritdoc />
        public long Revision => _revision;

        public EffectService(
            EffectDefinition[] definitions = null,
            IAttributeCommand attributeCommand = null,
            IEffectApplicationResolver applicationResolver = null,
            IEffectImmunityResolver immunityResolver = null,
            IEventBus eventBus = null)
        {
            SetDefinitions(definitions);
            _attributeCommand = attributeCommand;
            _applicationResolver = applicationResolver;
            _immunityResolver = immunityResolver;
            _eventBus = eventBus;
        }

        /// <inheritdoc />
        public void SetDefinitions(EffectDefinition[] definitions)
        {
            _definitions.Clear();
            if (definitions == null)
            {
                return;
            }

            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.EffectId))
                {
                    continue;
                }

                if (!_definitions.ContainsKey(definition.EffectId))
                {
                    _definitions.Add(definition.EffectId, definition);
                }
            }
        }

        /// <inheritdoc />
        public void SetTagCatalog(EffectTagCatalog tagCatalog)
        {
            _tagCatalog = tagCatalog;
        }

        /// <inheritdoc />
        public void SetAttributeCommand(IAttributeCommand attributeCommand)
        {
            _attributeCommand = attributeCommand;
            ReapplyPendingModifiers(true);
        }

        /// <inheritdoc />
        public void SetApplicationResolver(IEffectApplicationResolver resolver)
        {
            _applicationResolver = resolver;
        }

        /// <inheritdoc />
        public void SetImmunityResolver(IEffectImmunityResolver resolver)
        {
            _immunityResolver = resolver;
        }

        /// <inheritdoc />
        public void SetEventBus(IEventBus eventBus, EventChannel lifecycleEventChannel, bool publishLifecycleEvents)
        {
            _eventBus = eventBus;
            _lifecycleEventChannel = lifecycleEventChannel;
            _publishLifecycleEvents = publishLifecycleEvents;
        }

        /// <inheritdoc />
        public bool HasEffect(string actorId, string effectId)
        {
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(effectId))
            {
                return false;
            }

            return TryFindEffect(actorId, state => string.Equals(state.EffectId, effectId, StringComparison.Ordinal), out _);
        }

        /// <inheritdoc />
        public bool HasEffectWithTag(string actorId, string tag)
        {
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            return TryFindEffect(actorId, state => ContainsString(state.Tags, tag), out _);
        }

        /// <inheritdoc />
        public ActiveEffectSnapshot[] GetEffects(string actorId)
        {
            if (!_effectsByActor.TryGetValue(actorId ?? string.Empty, out var list) || list.Count == 0)
            {
                return Array.Empty<ActiveEffectSnapshot>();
            }

            var result = new ActiveEffectSnapshot[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i]?.ToSnapshot();
            }

            return result;
        }

        /// <inheritdoc />
        public ActiveEffectSnapshot[] GetEffectsByTag(string actorId, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || !_effectsByActor.TryGetValue(actorId ?? string.Empty, out var list) || list.Count == 0)
            {
                return Array.Empty<ActiveEffectSnapshot>();
            }

            _changedSnapshotBuffer.Clear();
            for (var i = 0; i < list.Count; i++)
            {
                var state = list[i];
                if (state != null && ContainsString(state.Tags, tag))
                {
                    _changedSnapshotBuffer.Add(state.ToSnapshot());
                }
            }

            return ToSnapshotArray(_changedSnapshotBuffer);
        }

        /// <inheritdoc />
        public bool TryGetEffect(string actorId, string effectInstanceId, out ActiveEffectSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(effectInstanceId))
            {
                return false;
            }

            if (!TryFindEffect(actorId, state => string.Equals(state.EffectInstanceId, effectInstanceId, StringComparison.Ordinal), out var effect))
            {
                return false;
            }

            snapshot = effect.ToSnapshot();
            return true;
        }

        /// <inheritdoc />
        public float GetRemainingSeconds(string actorId, string effectInstanceId)
        {
            return TryFindEffect(actorId, state => string.Equals(state.EffectInstanceId, effectInstanceId, StringComparison.Ordinal), out var effect)
                ? Math.Max(0f, effect.RemainingSeconds)
                : 0f;
        }

        /// <inheritdoc />
        public int GetStackCount(string actorId, string effectInstanceId)
        {
            return TryFindEffect(actorId, state => string.Equals(state.EffectInstanceId, effectInstanceId, StringComparison.Ordinal), out var effect)
                ? Math.Max(1, effect.StackCount)
                : 0;
        }

        /// <inheritdoc />
        public EffectApplyResult ApplyEffect(EffectApplyRequest request)
        {
            if (!ValidateApplyRequest(request, out var definition, out var failed))
            {
                return failed;
            }

            var list = GetOrCreateActorList(request.OwnerActorId);
            var stackRule = definition.StackRule != null ? definition.StackRule.Clone() : new EffectStackRuleData();
            if (stackRule.MaxStack < 1)
            {
                stackRule.MaxStack = 1;
            }

            var sameEffect = FindSameEffect(list, request.EffectId, stackRule.Mode);
            var context = BuildApplicationContext(request, definition, list, sameEffect != null);
            if (!request.IgnoreApplicationResolver && _applicationResolver != null)
            {
                var decision = _applicationResolver.CanApply(in context);
                if (!decision.Allowed)
                {
                    return EffectApplyResult.Failed(decision.FailureReason, request.OwnerActorId, request.EffectId, decision.Message);
                }
            }

            if (!request.IgnoreImmunityResolver && _immunityResolver != null)
            {
                var decision = _immunityResolver.CanApply(in context);
                if (!decision.Allowed)
                {
                    return EffectApplyResult.Failed(decision.FailureReason, request.OwnerActorId, request.EffectId, decision.Message);
                }
            }

            if (HasModifierTemplates(definition) && _attributeCommand == null)
            {
                return EffectApplyResult.Failed(
                    EffectApplyFailureReason.AttributeServiceMissing,
                    request.OwnerActorId,
                    request.EffectId,
                    "效果需要写入属性修饰器，但 AttributeCommand 未注入。");
            }

            return sameEffect == null
                ? CreateNewEffect(request, definition, list, stackRule)
                : ApplyStackRule(request, definition, list, sameEffect, stackRule);
        }

        /// <inheritdoc />
        public EffectOperationResult RemoveEffect(string actorId, string effectInstanceId, EffectRemoveReason reason)
        {
            if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(effectInstanceId))
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.InvalidRequest, actorId, "ActorId 或 EffectInstanceId 为空。");
            }

            if (!_effectsByActor.TryGetValue(actorId, out var list))
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.EffectNotFound, actorId, "找不到指定 Actor 的效果列表。");
            }

            for (var i = 0; i < list.Count; i++)
            {
                var state = list[i];
                if (state == null || !string.Equals(state.EffectInstanceId, effectInstanceId, StringComparison.Ordinal))
                {
                    continue;
                }

                var snapshot = state.ToSnapshot();
                var cleanupFailed = !CleanupModifiers(state);
                list.RemoveAt(i);
                RemoveEmptyActorList(actorId, list);
                BumpRevision();
                PublishRemoved(snapshot, reason);

                return cleanupFailed
                    ? EffectOperationResult.Success(actorId, new[] { snapshot }, "效果已移除，但属性修饰器清理失败。")
                    : EffectOperationResult.Success(actorId, new[] { snapshot });
            }

            return EffectOperationResult.Failed(EffectOperationFailureReason.EffectNotFound, actorId, "找不到指定效果。");
        }

        /// <inheritdoc />
        public EffectOperationResult RemoveEffectsBySource(string actorId, string sourceActorId, string sourceModule = null)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.InvalidRequest, actorId, "ActorId 为空。");
            }

            if (string.IsNullOrWhiteSpace(sourceActorId) && string.IsNullOrWhiteSpace(sourceModule))
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.InvalidRequest, actorId, "SourceActorId 和 SourceModule 不能同时为空。");
            }

            return RemoveWhere(
                actorId,
                state => (string.IsNullOrWhiteSpace(sourceActorId) || string.Equals(state.SourceActorId, sourceActorId, StringComparison.Ordinal))
                         && (string.IsNullOrWhiteSpace(sourceModule) || string.Equals(state.SourceModule, sourceModule, StringComparison.Ordinal)),
                EffectRemoveReason.SourceRemoved,
                skipDispellableCheck: true);
        }

        /// <inheritdoc />
        public EffectOperationResult DispelEffects(EffectDispelRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ActorId))
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.InvalidRequest, null, "驱散请求为空或 ActorId 为空。");
            }

            var maxCount = request.MaxCount <= 0 ? int.MaxValue : request.MaxCount;
            var removedCount = 0;
            return RemoveWhere(
                request.ActorId,
                state =>
                {
                    if (removedCount >= maxCount || state == null || !state.IsDispellable)
                    {
                        return false;
                    }

                    if (request.TargetPolarity != EffectPolarity.Neutral && state.Polarity != request.TargetPolarity)
                    {
                        return false;
                    }

                    if (!ContainsAllTags(state.Tags, request.RequiredTags))
                    {
                        return false;
                    }

                    removedCount++;
                    return true;
                },
                EffectRemoveReason.Dispelled,
                skipDispellableCheck: false);
        }

        /// <inheritdoc />
        public EffectOperationResult ClearEffects(string actorId, EffectRemoveReason reason)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.InvalidRequest, actorId, "ActorId 为空。");
            }

            return RemoveWhere(actorId, _ => true, reason, skipDispellableCheck: true);
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || _effectsByActor.Count == 0)
            {
                return;
            }

            _expiredBuffer.Clear();
            foreach (var pair in _effectsByActor)
            {
                var list = pair.Value;
                for (var i = 0; i < list.Count; i++)
                {
                    var state = list[i];
                    if (state == null || state.IsMissingDefinition || !IsDurationEffect(state))
                    {
                        continue;
                    }

                    state.RemainingSeconds -= deltaTime;
                    if (state.RemainingSeconds <= 0f)
                    {
                        _expiredBuffer.Add(state);
                    }
                }
            }

            for (var i = 0; i < _expiredBuffer.Count; i++)
            {
                var state = _expiredBuffer[i];
                if (state != null)
                {
                    RemoveEffect(state.OwnerActorId, state.EffectInstanceId, EffectRemoveReason.Expired);
                }
            }

            _expiredBuffer.Clear();
        }

        /// <inheritdoc />
        public EffectSaveData ExportSnapshot()
        {
            var owners = new List<EffectOwnerSnapshot>(_effectsByActor.Count);
            foreach (var pair in _effectsByActor)
            {
                var list = pair.Value;
                if (list == null || list.Count == 0)
                {
                    continue;
                }

                var effects = new ActiveEffectSnapshot[list.Count];
                for (var i = 0; i < list.Count; i++)
                {
                    effects[i] = list[i]?.ToSnapshot();
                }

                owners.Add(new EffectOwnerSnapshot
                {
                    ActorId = pair.Key,
                    Effects = effects
                });
            }

            return new EffectSaveData
            {
                Version = 1,
                Revision = _revision,
                Owners = owners.ToArray()
            };
        }

        /// <inheritdoc />
        public EffectOperationResult ImportSnapshot(EffectSaveData saveData)
        {
            if (saveData == null)
            {
                return EffectOperationResult.Failed(EffectOperationFailureReason.ImportInvalid, null, "效果存档数据为空。");
            }

            var imported = new Dictionary<string, List<ActiveEffectRuntimeState>>(StringComparer.Ordinal);
            if (saveData.Owners != null)
            {
                for (var i = 0; i < saveData.Owners.Length; i++)
                {
                    ImportOwner(saveData.Owners[i], imported);
                }
            }

            CleanupAllModifiers();
            _effectsByActor.Clear();
            foreach (var pair in imported)
            {
                _effectsByActor[pair.Key] = pair.Value;
            }

            _revision = Math.Max(0L, saveData.Revision);
            ReapplyPendingModifiers(false);
            return EffectOperationResult.Success(null, null, "效果快照导入完成。");
        }

        private bool ValidateApplyRequest(EffectApplyRequest request, out EffectDefinition definition, out EffectApplyResult failed)
        {
            definition = null;
            failed = null;
            if (request == null)
            {
                failed = EffectApplyResult.Failed(EffectApplyFailureReason.InvalidRequest, null, null, "效果施加请求为空。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.OwnerActorId))
            {
                failed = EffectApplyResult.Failed(EffectApplyFailureReason.OwnerActorMissing, request.OwnerActorId, request.EffectId, "OwnerActorId 为空。");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.EffectId))
            {
                failed = EffectApplyResult.Failed(EffectApplyFailureReason.InvalidRequest, request.OwnerActorId, request.EffectId, "EffectId 为空。");
                return false;
            }

            if (!_definitions.TryGetValue(request.EffectId, out definition) || definition == null)
            {
                failed = EffectApplyResult.Failed(EffectApplyFailureReason.DefinitionMissing, request.OwnerActorId, request.EffectId, "找不到效果配置。");
                return false;
            }

            return true;
        }

        private EffectApplyResult CreateNewEffect(
            EffectApplyRequest request,
            EffectDefinition definition,
            List<ActiveEffectRuntimeState> list,
            EffectStackRuleData stackRule)
        {
            var state = BuildRuntimeState(request, definition, stackRule);
            if (!ApplyModifiers(state, definition, stackRule))
            {
                return EffectApplyResult.Failed(
                    EffectApplyFailureReason.ModifierApplyFailed,
                    request.OwnerActorId,
                    request.EffectId,
                    "写入 AttributeModifier 失败。");
            }

            list.Add(state);
            BumpRevision();
            var snapshot = state.ToSnapshot();
            PublishApplied(snapshot);
            return EffectApplyResult.Success(
                request.OwnerActorId,
                request.EffectId,
                state.EffectInstanceId,
                snapshot,
                createdNewInstance: true,
                stackChanged: false);
        }

        private EffectApplyResult ApplyStackRule(
            EffectApplyRequest request,
            EffectDefinition definition,
            List<ActiveEffectRuntimeState> list,
            ActiveEffectRuntimeState sameEffect,
            EffectStackRuleData stackRule)
        {
            switch (stackRule.Mode)
            {
                case EffectStackMode.Reject:
                    return EffectApplyResult.Failed(EffectApplyFailureReason.AlreadyExists, request.OwnerActorId, request.EffectId, "同类效果已存在。");

                case EffectStackMode.ReplaceOld:
                    RemoveEffect(sameEffect.OwnerActorId, sameEffect.EffectInstanceId, EffectRemoveReason.Replaced);
                    return CreateNewEffect(request, definition, GetOrCreateActorList(request.OwnerActorId), stackRule);

                case EffectStackMode.AddStackRefreshDuration:
                case EffectStackMode.AddStackKeepDuration:
                    return AddStack(request, definition, sameEffect, stackRule, stackRule.Mode == EffectStackMode.AddStackRefreshDuration);

                case EffectStackMode.RefreshDuration:
                default:
                    return RefreshDuration(request, definition, sameEffect, stackRule);
            }
        }

        private EffectApplyResult RefreshDuration(
            EffectApplyRequest request,
            EffectDefinition definition,
            ActiveEffectRuntimeState state,
            EffectStackRuleData stackRule)
        {
            var oldDuration = state.DurationSeconds;
            var oldRemaining = state.RemainingSeconds;
            state.DurationSeconds = ResolveDuration(request, definition);
            state.RemainingSeconds = ResolveInitialRemainingSeconds(definition, state.DurationSeconds);
            if (!ApplyModifiers(state, definition, stackRule))
            {
                state.DurationSeconds = oldDuration;
                state.RemainingSeconds = oldRemaining;
                return EffectApplyResult.Failed(EffectApplyFailureReason.ModifierApplyFailed, request.OwnerActorId, request.EffectId, "刷新属性修饰器失败。");
            }

            BumpRevision();
            var snapshot = state.ToSnapshot();
            PublishApplied(snapshot);
            return EffectApplyResult.Success(request.OwnerActorId, request.EffectId, state.EffectInstanceId, snapshot, false, false);
        }

        private EffectApplyResult AddStack(
            EffectApplyRequest request,
            EffectDefinition definition,
            ActiveEffectRuntimeState state,
            EffectStackRuleData stackRule,
            bool refreshDuration)
        {
            var oldStack = Math.Max(1, state.StackCount);
            var oldDuration = state.DurationSeconds;
            var oldRemaining = state.RemainingSeconds;
            var addStack = Math.Max(1, request.StackCount);
            state.StackCount = Math.Min(stackRule.MaxStack, oldStack + addStack);
            if (refreshDuration)
            {
                state.DurationSeconds = ResolveDuration(request, definition);
                state.RemainingSeconds = ResolveInitialRemainingSeconds(definition, state.DurationSeconds);
            }

            if (!ApplyModifiers(state, definition, stackRule))
            {
                state.StackCount = oldStack;
                state.DurationSeconds = oldDuration;
                state.RemainingSeconds = oldRemaining;
                return EffectApplyResult.Failed(EffectApplyFailureReason.ModifierApplyFailed, request.OwnerActorId, request.EffectId, "叠层属性修饰器刷新失败。");
            }

            BumpRevision();
            var snapshot = state.ToSnapshot();
            if (state.StackCount != oldStack)
            {
                PublishStackChanged(snapshot, oldStack, state.StackCount);
            }

            return EffectApplyResult.Success(request.OwnerActorId, request.EffectId, state.EffectInstanceId, snapshot, false, state.StackCount != oldStack);
        }

        private ActiveEffectRuntimeState BuildRuntimeState(
            EffectApplyRequest request,
            EffectDefinition definition,
            EffectStackRuleData stackRule)
        {
            var duration = ResolveDuration(request, definition);
            return new ActiveEffectRuntimeState
            {
                EffectInstanceId = EffectProtocolUtility.CreateEffectInstanceId(),
                EffectId = definition.EffectId,
                OwnerActorId = request.OwnerActorId,
                SourceActorId = request.SourceActorId,
                SourceModule = request.SourceModule,
                Type = definition.Type,
                Polarity = definition.Polarity,
                DurationSeconds = duration,
                RemainingSeconds = ResolveInitialRemainingSeconds(definition, duration),
                StackCount = Math.Min(stackRule.MaxStack, Math.Max(1, request.StackCount)),
                IsDispellable = definition.IsDispellable,
                IsMissingDefinition = false,
                IsPendingModifierApply = false,
                Tags = CloneStringArray(definition.Tags),
                CustomData = EffectCustomDataEntry.CloneArray(request.CustomData)
            };
        }

        private EffectApplicationContext BuildApplicationContext(
            EffectApplyRequest request,
            EffectDefinition definition,
            List<ActiveEffectRuntimeState> list,
            bool hasSameEffect)
        {
            var snapshots = new ActiveEffectSnapshot[list.Count];
            var stackCount = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var state = list[i];
                snapshots[i] = state?.ToSnapshot();
                if (state != null && string.Equals(state.EffectId, request.EffectId, StringComparison.Ordinal))
                {
                    stackCount += Math.Max(1, state.StackCount);
                }
            }

            return new EffectApplicationContext(request.Clone(), definition, snapshots, stackCount, hasSameEffect);
        }

        private bool ApplyModifiers(ActiveEffectRuntimeState state, EffectDefinition definition, EffectStackRuleData stackRule)
        {
            if (state == null || definition == null || !HasModifierTemplates(definition))
            {
                return true;
            }

            if (_attributeCommand == null)
            {
                state.IsPendingModifierApply = true;
                return false;
            }

            var templates = definition.ModifierTemplates;
            for (var i = 0; i < templates.Length; i++)
            {
                var template = templates[i];
                if (template == null)
                {
                    continue;
                }

                var result = _attributeCommand.AddModifier(
                    state.OwnerActorId,
                    template.BuildModifier(state.EffectInstanceId, state.StackCount, stackRule.ScaleModifiersByStack));

                if (result == null || !result.Succeeded)
                {
                    ForceCleanupModifiersBySource(state);
                    state.IsPendingModifierApply = true;
                    return false;
                }
            }

            state.IsPendingModifierApply = false;
            return true;
        }

        private bool CleanupModifiers(ActiveEffectRuntimeState state)
        {
            if (state == null || _attributeCommand == null || state.IsMissingDefinition || state.IsPendingModifierApply)
            {
                return true;
            }

            if (!_definitions.TryGetValue(state.EffectId, out var definition) || !HasModifierTemplates(definition))
            {
                return true;
            }

            var sourceId = state.ModifierSourceId;
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return true;
            }

            var result = _attributeCommand.RemoveModifiersBySource(state.OwnerActorId, sourceId);
            return result == null || result.Succeeded;
        }

        private void ForceCleanupModifiersBySource(ActiveEffectRuntimeState state)
        {
            if (state == null || _attributeCommand == null)
            {
                return;
            }

            var sourceId = state.ModifierSourceId;
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            _attributeCommand.RemoveModifiersBySource(state.OwnerActorId, sourceId);
        }

        private void CleanupAllModifiers()
        {
            foreach (var pair in _effectsByActor)
            {
                var list = pair.Value;
                if (list == null)
                {
                    continue;
                }

                for (var i = 0; i < list.Count; i++)
                {
                    CleanupModifiers(list[i]);
                }
            }
        }

        private void ReapplyPendingModifiers(bool bumpRevision)
        {
            if (_attributeCommand == null)
            {
                return;
            }

            var changed = false;
            foreach (var pair in _effectsByActor)
            {
                var list = pair.Value;
                for (var i = 0; list != null && i < list.Count; i++)
                {
                    var state = list[i];
                    if (state == null || state.IsMissingDefinition || !_definitions.TryGetValue(state.EffectId, out var definition))
                    {
                        continue;
                    }

                    var stackRule = definition.StackRule != null ? definition.StackRule.Clone() : new EffectStackRuleData();
                    if (ApplyModifiers(state, definition, stackRule))
                    {
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                if (bumpRevision)
                {
                    BumpRevision();
                }
            }
        }

        private EffectOperationResult RemoveWhere(
            string actorId,
            Predicate<ActiveEffectRuntimeState> predicate,
            EffectRemoveReason reason,
            bool skipDispellableCheck)
        {
            if (!_effectsByActor.TryGetValue(actorId, out var list) || list == null || list.Count == 0)
            {
                return EffectOperationResult.Success(actorId, Array.Empty<ActiveEffectSnapshot>());
            }

            _changedSnapshotBuffer.Clear();
            var cleanupFailed = false;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var state = list[i];
                if (state == null || predicate == null || !predicate(state))
                {
                    continue;
                }

                if (!skipDispellableCheck && !state.IsDispellable)
                {
                    continue;
                }

                var snapshot = state.ToSnapshot();
                if (!CleanupModifiers(state))
                {
                    cleanupFailed = true;
                }

                list.RemoveAt(i);
                _changedSnapshotBuffer.Add(snapshot);
                PublishRemoved(snapshot, reason);
            }

            if (_changedSnapshotBuffer.Count == 0)
            {
                return EffectOperationResult.Success(actorId, Array.Empty<ActiveEffectSnapshot>());
            }

            RemoveEmptyActorList(actorId, list);
            BumpRevision();
            var changed = ToSnapshotArray(_changedSnapshotBuffer);
            return cleanupFailed
                ? EffectOperationResult.Success(actorId, changed, "效果已移除，但部分属性修饰器清理失败。")
                : EffectOperationResult.Success(actorId, changed);
        }

        private void ImportOwner(
            EffectOwnerSnapshot ownerSnapshot,
            Dictionary<string, List<ActiveEffectRuntimeState>> target)
        {
            if (ownerSnapshot == null || string.IsNullOrWhiteSpace(ownerSnapshot.ActorId) || ownerSnapshot.Effects == null)
            {
                return;
            }

            if (!target.TryGetValue(ownerSnapshot.ActorId, out var list))
            {
                list = new List<ActiveEffectRuntimeState>();
                target.Add(ownerSnapshot.ActorId, list);
            }

            for (var i = 0; i < ownerSnapshot.Effects.Length; i++)
            {
                var state = ActiveEffectRuntimeState.FromSnapshot(ownerSnapshot.Effects[i]);
                if (state == null || string.IsNullOrWhiteSpace(state.EffectInstanceId) || string.IsNullOrWhiteSpace(state.EffectId))
                {
                    continue;
                }

                state.OwnerActorId = string.IsNullOrWhiteSpace(state.OwnerActorId) ? ownerSnapshot.ActorId : state.OwnerActorId;
                if (!_definitions.TryGetValue(state.EffectId, out var definition) || definition == null)
                {
                    state.IsMissingDefinition = true;
                    state.IsPendingModifierApply = false;
                }
                else
                {
                    state.Type = definition.Type;
                    state.Polarity = definition.Polarity;
                    state.IsDispellable = definition.IsDispellable;
                    state.Tags = CloneStringArray(definition.Tags);
                    state.IsMissingDefinition = false;
                    state.IsPendingModifierApply = HasModifierTemplates(definition);
                }

                list.Add(state);
            }
        }

        private List<ActiveEffectRuntimeState> GetOrCreateActorList(string actorId)
        {
            if (!_effectsByActor.TryGetValue(actorId, out var list))
            {
                list = new List<ActiveEffectRuntimeState>();
                _effectsByActor.Add(actorId, list);
            }

            return list;
        }

        private ActiveEffectRuntimeState FindSameEffect(List<ActiveEffectRuntimeState> list, string effectId, EffectStackMode stackMode)
        {
            if (stackMode == EffectStackMode.IndependentInstance || list == null)
            {
                return null;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var state = list[i];
                if (state != null && string.Equals(state.EffectId, effectId, StringComparison.Ordinal))
                {
                    return state;
                }
            }

            return null;
        }

        private bool TryFindEffect(string actorId, Predicate<ActiveEffectRuntimeState> predicate, out ActiveEffectRuntimeState effect)
        {
            effect = null;
            if (!_effectsByActor.TryGetValue(actorId ?? string.Empty, out var list) || predicate == null)
            {
                return false;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var state = list[i];
                if (state != null && predicate(state))
                {
                    effect = state;
                    return true;
                }
            }

            return false;
        }

        private void RemoveEmptyActorList(string actorId, List<ActiveEffectRuntimeState> list)
        {
            if (list != null && list.Count == 0)
            {
                _effectsByActor.Remove(actorId);
            }
        }

        private void BumpRevision()
        {
            _revision = _revision == long.MaxValue ? long.MaxValue : _revision + 1;
        }

        private void PublishApplied(ActiveEffectSnapshot snapshot)
        {
            if (!_publishLifecycleEvents || _eventBus == null || snapshot == null)
            {
                return;
            }

            _eventBus.Publish(
                new EffectAppliedEvent(
                    snapshot.OwnerActorId,
                    snapshot.EffectInstanceId,
                    snapshot.EffectId,
                    snapshot.SourceActorId,
                    snapshot.SourceModule,
                    snapshot.StackCount),
                _lifecycleEventChannel);
        }

        private void PublishRemoved(ActiveEffectSnapshot snapshot, EffectRemoveReason reason)
        {
            if (!_publishLifecycleEvents || _eventBus == null || snapshot == null)
            {
                return;
            }

            _eventBus.Publish(
                new EffectRemovedEvent(snapshot.OwnerActorId, snapshot.EffectInstanceId, snapshot.EffectId, reason),
                _lifecycleEventChannel);
        }

        private void PublishStackChanged(ActiveEffectSnapshot snapshot, int oldStack, int newStack)
        {
            if (!_publishLifecycleEvents || _eventBus == null || snapshot == null)
            {
                return;
            }

            _eventBus.Publish(
                new EffectStackChangedEvent(snapshot.OwnerActorId, snapshot.EffectInstanceId, snapshot.EffectId, oldStack, newStack),
                _lifecycleEventChannel);
        }

        private static bool HasModifierTemplates(EffectDefinition definition)
        {
            return definition?.ModifierTemplates != null && definition.ModifierTemplates.Length > 0;
        }

        private static bool IsDurationEffect(ActiveEffectRuntimeState state)
        {
            return state.DurationSeconds > 0f && state.RemainingSeconds > 0f;
        }

        private static float ResolveDuration(EffectApplyRequest request, EffectDefinition definition)
        {
            return request.DurationOverrideSeconds > 0f ? request.DurationOverrideSeconds : Math.Max(0f, definition.DurationSeconds);
        }

        private static float ResolveInitialRemainingSeconds(EffectDefinition definition, float duration)
        {
            return definition.DurationMode == EffectDurationMode.Duration ? Math.Max(0f, duration) : 0f;
        }

        private static bool ContainsString(string[] values, string target)
        {
            if (values == null || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            for (var i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAllTags(string[] source, string[] requiredTags)
        {
            if (requiredTags == null || requiredTags.Length == 0)
            {
                return true;
            }

            for (var i = 0; i < requiredTags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(requiredTags[i]) && !ContainsString(source, requiredTags[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] CloneStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }

        private static ActiveEffectSnapshot[] ToSnapshotArray(List<ActiveEffectSnapshot> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<ActiveEffectSnapshot>();
            }

            var result = new ActiveEffectSnapshot[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }
}
