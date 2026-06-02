using System;
using System.Collections.Generic;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaAttribute.Result;
using NiumaAttribute.Service;
using NiumaEffect.Config;
using NiumaEffect.Data;
using NiumaEffect.Enum;
using NiumaEffect.Request;
using NiumaEffect.Result;
using NiumaEffect.Service;
using UnityEngine;

namespace NiumaEffect.Debugging
{
    /// <summary>
    /// NiumaEffect 基础测试入口。
    /// 该组件只用于开发阶段在 Unity 场景内手动验证核心流程，不参与正式业务。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EffectBasicTestRunner : MonoBehaviour
    {
        private const string ActorId = "player";
        private const string SourceActorId = "debug_caster";
        private const string AttackAttributeId = "attack";
        private const string DefenseAttributeId = "defense";

        private const string MightEffectId = "test_might";
        private const string PoisonEffectId = "test_poison";
        private const string CurseEffectId = "test_curse";
        private const string RejectEffectId = "test_reject";
        private const string PartialFailureEffectId = "test_partial_failure";

        [Header("测试行为")]
        [Tooltip("运行测试后是否在 Console 输出每一条通过信息。关闭后只输出最终结果和失败原因。")]
        [SerializeField] private bool verboseLog = true;

        [Header("最近一次结果")]
        [Tooltip("最近一次基础测试是否全部通过。")]
        [SerializeField] private bool lastRunSucceeded;

        [Tooltip("最近一次通过的检查数量。")]
        [SerializeField] private int passedCheckCount;

        [Tooltip("最近一次失败的检查数量。")]
        [SerializeField] private int failedCheckCount;

        [Tooltip("最近一次测试报告。")]
        [TextArea(8, 24)]
        [SerializeField] private string lastReport;

        private readonly List<string> _reportLines = new List<string>();
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        /// <summary>
        /// 运行第 7 阶段基础测试。
        /// </summary>
        [ContextMenu("NiumaEffectTest/运行基础测试")]
        public void RunBasicTests()
        {
            ResetReport();

            RunCase("添加 Buff 与 AttributeModifier 写入", TestApplyEffectAndModifier);
            RunCase("叠层刷新与 Modifier 覆盖", TestStackRefreshAndModifierScaling);
            RunCase("Tick 到期移除与 Revision 策略", TestTickExpirationAndModifierCleanup);
            RunCase("驱散只移除可驱散效果", TestDispelOnlyDispellableEffects);
            RunCase("导出导入后重新应用 Modifier 且不重复", TestExportImportAndModifierReapply);
            RunCase("缺失配置导入保护", TestMissingDefinitionImport);
            RunCase("部分 Modifier 写入失败时清理孤立 Modifier", TestPartialModifierApplyFailureCleanup);
            RunCase("Reject 叠层规则拒绝重复添加", TestRejectStackRule);
            RunCase("UI ViewData 基础克隆稳定", TestViewDataClone);

            lastRunSucceeded = failedCheckCount == 0;
            lastReport = string.Join(Environment.NewLine, _reportLines);

            var summary = $"[NiumaEffectTest] 基础测试结束：Passed={passedCheckCount}, Failed={failedCheckCount}";
            if (lastRunSucceeded)
            {
                UnityEngine.Debug.Log(summary, this);
            }
            else
            {
                UnityEngine.Debug.LogError(summary + Environment.NewLine + lastReport, this);
            }

            ReleaseCreatedAssets();
        }

        /// <summary>
        /// 清空最近一次测试报告。
        /// </summary>
        [ContextMenu("NiumaEffectTest/清空测试报告")]
        public void ClearReport()
        {
            lastRunSucceeded = false;
            passedCheckCount = 0;
            failedCheckCount = 0;
            lastReport = string.Empty;
            _reportLines.Clear();
        }

        private void TestApplyEffectAndModifier()
        {
            var attribute = new FakeAttributeCommand();
            var service = CreateService(attribute);

            var result = service.ApplyEffect(CreateApplyRequest(MightEffectId));
            ExpectApplySuccess("添加攻击 Buff 成功", result);
            Expect(service.HasEffect(ActorId, MightEffectId), "添加后可以查询到攻击 Buff");
            ExpectEqual(1, service.GetEffects(ActorId).Length, "Actor 身上有 1 个效果");

            var sourceId = EffectProtocolUtility.BuildModifierSourceId(result.EffectInstanceId);
            Expect(attribute.TryGetModifier(ActorId, sourceId, "attack_add", out var modifier), "攻击 Buff 写入 AttributeModifier");
            ExpectApproximately(5f, modifier.Value, "攻击 Buff Modifier 数值正确");
            ExpectEqual(1, attribute.ModifierCount, "Attribute 中只有 1 个 Modifier");
        }

        private void TestStackRefreshAndModifierScaling()
        {
            var attribute = new FakeAttributeCommand();
            var service = CreateService(attribute);

            var first = service.ApplyEffect(CreateApplyRequest(MightEffectId, stackCount: 1));
            ExpectApplySuccess("第一次添加攻击 Buff", first);

            service.Tick(0.4f);
            var second = service.ApplyEffect(CreateApplyRequest(MightEffectId, stackCount: 1));
            ExpectApplySuccess("第二次添加攻击 Buff 触发叠层", second);
            Expect(!second.CreatedNewInstance, "叠层不会创建新实例");
            Expect(second.StackChanged, "叠层结果标记 StackChanged");
            ExpectEqual(first.EffectInstanceId, second.EffectInstanceId, "叠层沿用同一个效果实例");
            ExpectEqual(2, second.Effect.StackCount, "叠层后层数为 2");
            ExpectApproximately(1f, second.Effect.RemainingSeconds, "AddStackRefreshDuration 会刷新剩余时间");

            var sourceId = EffectProtocolUtility.BuildModifierSourceId(second.EffectInstanceId);
            Expect(attribute.TryGetModifier(ActorId, sourceId, "attack_add", out var modifier), "叠层后 Modifier 仍存在");
            ExpectApproximately(10f, modifier.Value, "ScaleModifiersByStack 会按层数放大 Modifier.Value");
            ExpectEqual(1, attribute.CountBySource(ActorId, sourceId), "同 SourceId + ModifierId 覆盖而不是重复添加");
        }

        private void TestTickExpirationAndModifierCleanup()
        {
            var attribute = new FakeAttributeCommand();
            var service = CreateService(attribute);

            var result = service.ApplyEffect(CreateApplyRequest(MightEffectId));
            ExpectApplySuccess("添加短持续 Buff", result);
            var sourceId = EffectProtocolUtility.BuildModifierSourceId(result.EffectInstanceId);
            var revisionAfterApply = service.Revision;

            service.Tick(0.2f);
            ExpectEqual(revisionAfterApply, service.Revision, "倒计时递减本身不递增 Revision");
            Expect(service.HasEffect(ActorId, MightEffectId), "未到期时效果仍存在");

            service.Tick(2f);
            Expect(!service.HasEffect(ActorId, MightEffectId), "到期后效果被移除");
            Expect(!attribute.HasSource(ActorId, sourceId), "到期后 Modifier 被清理");
            Expect(service.Revision > revisionAfterApply, "过期移除时才递增 Revision");
        }

        private void TestDispelOnlyDispellableEffects()
        {
            var attribute = new FakeAttributeCommand();
            var service = CreateService(attribute);

            var poison = service.ApplyEffect(CreateApplyRequest(PoisonEffectId));
            var curse = service.ApplyEffect(CreateApplyRequest(CurseEffectId));
            ExpectApplySuccess("添加可驱散负面效果", poison);
            ExpectApplySuccess("添加不可驱散负面效果", curse);

            var dispel = service.DispelEffects(new EffectDispelRequest
            {
                ActorId = ActorId,
                TargetPolarity = EffectPolarity.Harmful,
                SourceActorId = SourceActorId,
                SourceModule = nameof(EffectBasicTestRunner)
            });

            ExpectOperationSuccess("驱散请求成功", dispel);
            ExpectEqual(1, dispel.ChangedEffects.Length, "驱散只移除 1 个可驱散效果");
            Expect(!service.HasEffect(ActorId, PoisonEffectId), "可驱散 Poison 被移除");
            Expect(service.HasEffect(ActorId, CurseEffectId), "不可驱散 Curse 保留");
        }

        private void TestExportImportAndModifierReapply()
        {
            var sourceAttribute = new FakeAttributeCommand();
            var sourceService = CreateService(sourceAttribute);

            ExpectApplySuccess("导出前第一次添加攻击 Buff", sourceService.ApplyEffect(CreateApplyRequest(MightEffectId)));
            ExpectApplySuccess("导出前第二次叠加攻击 Buff", sourceService.ApplyEffect(CreateApplyRequest(MightEffectId)));
            var snapshot = sourceService.ExportSnapshot();

            var restoredAttribute = new FakeAttributeCommand();
            var restoredService = CreateService(restoredAttribute);
            var importResult = restoredService.ImportSnapshot(snapshot);
            ExpectOperationSuccess("导入效果快照成功", importResult);

            var restoredEffects = restoredService.GetEffects(ActorId);
            ExpectEqual(1, restoredEffects.Length, "导入后恢复 1 个效果");
            ExpectEqual(2, restoredEffects[0].StackCount, "导入后层数保持一致");
            ExpectEqual(snapshot.Revision, restoredService.Revision, "导入后 Revision 继承存档");
            ExpectEqual(1, restoredAttribute.ModifierCount, "导入后重新应用 1 个 Modifier");

            var sourceId = EffectProtocolUtility.BuildModifierSourceId(restoredEffects[0].EffectInstanceId);
            Expect(restoredAttribute.TryGetModifier(ActorId, sourceId, "attack_add", out var modifier), "导入后 Modifier 可查询");
            ExpectApproximately(10f, modifier.Value, "导入后 Modifier 数值按层数恢复");

            ExpectOperationSuccess("重复导入同一快照成功", restoredService.ImportSnapshot(snapshot));
            ExpectEqual(1, restoredAttribute.ModifierCount, "重复导入不会产生重复 Modifier");
        }

        private void TestMissingDefinitionImport()
        {
            var attribute = new FakeAttributeCommand();
            var service = CreateService(attribute, Array.Empty<EffectDefinition>());
            var saveData = new EffectSaveData
            {
                Version = 1,
                Revision = 7L,
                Owners = new[]
                {
                    new EffectOwnerSnapshot
                    {
                        ActorId = ActorId,
                        Effects = new[]
                        {
                            new ActiveEffectSnapshot
                            {
                                EffectInstanceId = "missing_effect_instance",
                                EffectId = "missing_effect",
                                OwnerActorId = ActorId,
                                SourceActorId = SourceActorId,
                                SourceModule = nameof(EffectBasicTestRunner),
                                DurationSeconds = 10f,
                                RemainingSeconds = 3f,
                                StackCount = 1,
                                Type = EffectType.Buff,
                                Polarity = EffectPolarity.Beneficial,
                                IsDispellable = true,
                                Tags = new[] { "missing_test" },
                                CustomData = Array.Empty<EffectCustomDataEntry>()
                            }
                        }
                    }
                }
            };

            ExpectOperationSuccess("导入缺失配置效果快照成功", service.ImportSnapshot(saveData));
            var effects = service.GetEffects(ActorId);
            ExpectEqual(1, effects.Length, "缺失配置效果仍保留在运行时");
            Expect(effects[0].IsMissingDefinition, "缺失配置效果被标记为 MissingDefinition");
            ExpectEqual(0, attribute.ModifierCount, "缺失配置效果不会写入 Modifier");
            ExpectEqual(7L, service.Revision, "缺失配置导入后 Revision 继承存档");
        }

        private void TestPartialModifierApplyFailureCleanup()
        {
            var attribute = new FakeAttributeCommand { FailOnModifierId = "fail_second" };
            var service = CreateService(attribute);

            var result = service.ApplyEffect(CreateApplyRequest(PartialFailureEffectId));
            ExpectApplyFailed("第二个 Modifier 写入失败时施加效果失败", result, EffectApplyFailureReason.ModifierApplyFailed);
            ExpectEqual(0, service.GetEffects(ActorId).Length, "部分写入失败时效果不会进入运行时列表");
            ExpectEqual(0, attribute.ModifierCount, "部分写入失败时已写入的第一个 Modifier 会被强制清理");
            ExpectEqual(1, attribute.RemoveBySourceCallCount, "部分写入失败时调用 RemoveModifiersBySource 清理孤立 Modifier");
        }

        private void TestRejectStackRule()
        {
            var attribute = new FakeAttributeCommand();
            var service = CreateService(attribute);

            ExpectApplySuccess("第一次添加 Reject 效果", service.ApplyEffect(CreateApplyRequest(RejectEffectId)));
            var second = service.ApplyEffect(CreateApplyRequest(RejectEffectId));
            ExpectApplyFailed("Reject 规则拒绝重复添加", second, EffectApplyFailureReason.AlreadyExists);
            ExpectEqual(1, service.GetEffects(ActorId).Length, "Reject 失败后运行时仍只有 1 个效果");
        }

        private void TestViewDataClone()
        {
            var original = new NiumaEffect.ViewData.EffectPanelViewData
            {
                ActorId = ActorId,
                Revision = 3L,
                Effects = new[]
                {
                    new NiumaEffect.ViewData.EffectIconViewData
                    {
                        EffectInstanceId = "ui_effect_instance",
                        EffectId = MightEffectId,
                        DisplayName = "测试强击",
                        StackCount = 2,
                        DurationSeconds = 10f,
                        RemainingSeconds = 5f,
                        Progress01 = 0.5f,
                        Tags = new[] { "buff_attack" }
                    }
                }
            };

            var clone = original.Clone();
            clone.Effects[0].Tags[0] = "changed";

            ExpectEqual(ActorId, clone.ActorId, "EffectPanelViewData Clone 保留 ActorId");
            ExpectEqual(3L, clone.Revision, "EffectPanelViewData Clone 保留 Revision");
            ExpectEqual(1, clone.Effects.Length, "EffectPanelViewData Clone 保留效果数量");
            ExpectEqual("buff_attack", original.Effects[0].Tags[0], "EffectIconViewData Clone 会复制 Tags，避免 UI 反向污染");
        }

        private EffectService CreateService(FakeAttributeCommand attributeCommand)
        {
            return CreateService(attributeCommand, CreateDefinitions());
        }

        private static EffectService CreateService(FakeAttributeCommand attributeCommand, EffectDefinition[] definitions)
        {
            return new EffectService(definitions, attributeCommand);
        }

        private EffectDefinition[] CreateDefinitions()
        {
            return new[]
            {
                CreateDefinition(
                    MightEffectId,
                    "测试强击",
                    EffectType.Buff,
                    EffectPolarity.Beneficial,
                    true,
                    1f,
                    new[] { "buff_attack" },
                    EffectStackMode.AddStackRefreshDuration,
                    3,
                    true,
                    new[] { Modifier("attack_add", AttackAttributeId, 5f) }),
                CreateDefinition(
                    PoisonEffectId,
                    "测试中毒",
                    EffectType.Debuff,
                    EffectPolarity.Harmful,
                    true,
                    5f,
                    new[] { "debuff_poison" },
                    EffectStackMode.RefreshDuration,
                    1,
                    true,
                    new[] { Modifier("defense_down", DefenseAttributeId, -2f) }),
                CreateDefinition(
                    CurseEffectId,
                    "测试诅咒",
                    EffectType.Debuff,
                    EffectPolarity.Harmful,
                    false,
                    5f,
                    new[] { "debuff_curse" },
                    EffectStackMode.RefreshDuration,
                    1,
                    true,
                    Array.Empty<EffectModifierTemplateData>()),
                CreateDefinition(
                    RejectEffectId,
                    "测试拒绝重复",
                    EffectType.State,
                    EffectPolarity.Neutral,
                    true,
                    5f,
                    new[] { "state_reject" },
                    EffectStackMode.Reject,
                    1,
                    true,
                    Array.Empty<EffectModifierTemplateData>()),
                CreateDefinition(
                    PartialFailureEffectId,
                    "测试部分 Modifier 失败",
                    EffectType.Buff,
                    EffectPolarity.Beneficial,
                    true,
                    5f,
                    new[] { "buff_partial_failure" },
                    EffectStackMode.RefreshDuration,
                    1,
                    true,
                    new[]
                    {
                        Modifier("pass_first", AttackAttributeId, 1f),
                        Modifier("fail_second", DefenseAttributeId, 1f)
                    })
            };
        }

        private EffectDefinition CreateDefinition(
            string effectId,
            string displayName,
            EffectType type,
            EffectPolarity polarity,
            bool isDispellable,
            float durationSeconds,
            string[] tags,
            EffectStackMode stackMode,
            int maxStack,
            bool scaleByStack,
            EffectModifierTemplateData[] modifiers)
        {
            var definition = ScriptableObject.CreateInstance<EffectDefinition>();
            definition.EffectId = effectId;
            definition.DisplayName = displayName;
            definition.Description = displayName + "说明";
            definition.IconAddress = "debug/" + effectId;
            definition.Type = type;
            definition.Polarity = polarity;
            definition.DurationMode = EffectDurationMode.Duration;
            definition.DurationSeconds = durationSeconds;
            definition.IsDispellable = isDispellable;
            definition.Tags = tags ?? Array.Empty<string>();
            definition.StackRule = new EffectStackRuleData
            {
                Mode = stackMode,
                MaxStack = Math.Max(1, maxStack),
                ScaleModifiersByStack = scaleByStack
            };
            definition.ModifierTemplates = modifiers ?? Array.Empty<EffectModifierTemplateData>();
            _createdAssets.Add(definition);
            return definition;
        }

        private static EffectModifierTemplateData Modifier(string modifierId, string attributeId, float value)
        {
            return new EffectModifierTemplateData
            {
                ModifierId = modifierId,
                TargetAttributeId = attributeId,
                Operation = AttributeModifierOperation.Add,
                Layer = AttributeModifierLayer.Effect,
                Value = value,
                Priority = 0
            };
        }

        private static EffectApplyRequest CreateApplyRequest(string effectId, int stackCount = 1)
        {
            return new EffectApplyRequest
            {
                EffectId = effectId,
                OwnerActorId = ActorId,
                SourceActorId = SourceActorId,
                SourceModule = nameof(EffectBasicTestRunner),
                StackCount = stackCount,
                IgnoreApplicationResolver = true,
                IgnoreImmunityResolver = true,
                CustomData = Array.Empty<EffectCustomDataEntry>()
            };
        }

        private void RunCase(string name, Action action)
        {
            try
            {
                action();
                AddReportLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failedCheckCount++;
                var message = $"[FAIL] {name}：{exception.Message}";
                _reportLines.Add(message);
                UnityEngine.Debug.LogError($"[NiumaEffectTest] {message}", this);
            }
        }

        private void ExpectApplySuccess(string label, EffectApplyResult result)
        {
            Expect(result != null && result.Succeeded, $"{label}。Reason={result?.FailureReason}, Message={result?.Message}");
        }

        private void ExpectApplyFailed(string label, EffectApplyResult result, EffectApplyFailureReason expectedReason)
        {
            Expect(result != null && !result.Succeeded && result.FailureReason == expectedReason,
                $"{label}。Expected={expectedReason}, Actual={result?.FailureReason.ToString() ?? "<null>"}, Message={result?.Message}");
        }

        private void ExpectOperationSuccess(string label, EffectOperationResult result)
        {
            Expect(result != null && result.Succeeded, $"{label}。Reason={result?.FailureReason}, Message={result?.Message}");
        }

        private void ExpectApproximately(float expected, float actual, string label, float epsilon = 0.001f)
        {
            Expect(Math.Abs(expected - actual) <= epsilon, $"{label}。Expected={expected}, Actual={actual}");
        }

        private void ExpectEqual<T>(T expected, T actual, string label)
        {
            Expect(EqualityComparer<T>.Default.Equals(expected, actual), $"{label}。Expected={expected}, Actual={actual}");
        }

        private void Expect(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }

            passedCheckCount++;
            if (verboseLog)
            {
                AddReportLine($"[OK] {message}");
            }
        }

        private void AddReportLine(string line)
        {
            _reportLines.Add(line);
            if (verboseLog)
            {
                UnityEngine.Debug.Log($"[NiumaEffectTest] {line}", this);
            }
        }

        private void ResetReport()
        {
            lastRunSucceeded = false;
            passedCheckCount = 0;
            failedCheckCount = 0;
            lastReport = string.Empty;
            _reportLines.Clear();
            ReleaseCreatedAssets();
        }

        private void ReleaseCreatedAssets()
        {
            for (var i = 0; i < _createdAssets.Count; i++)
            {
                var asset = _createdAssets[i];
                if (asset == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(asset);
                }
                else
                {
                    DestroyImmediate(asset);
                }
            }

            _createdAssets.Clear();
        }

        private sealed class FakeAttributeCommand : IAttributeCommand
        {
            private readonly Dictionary<string, AttributeModifier> _modifiers = new Dictionary<string, AttributeModifier>(StringComparer.Ordinal);
            private long _revision;

            public string FailOnModifierId { get; set; }
            public int RemoveBySourceCallCount { get; private set; }
            public int ModifierCount => _modifiers.Count;

            public AttributeOperationResult CreateActor(string actorId, string sourceModule = null)
            {
                return AttributeOperationResult.Success(actorId, ++_revision);
            }

            public AttributeOperationResult RemoveActor(string actorId, string sourceModule = null)
            {
                return AttributeOperationResult.Success(actorId, ++_revision);
            }

            public AttributeOperationResult SetBaseValue(string actorId, string attributeId, float value, string sourceModule = null)
            {
                return AttributeOperationResult.Success(attributeId, ++_revision);
            }

            public AttributeOperationResult AddModifier(string actorId, AttributeModifier modifier)
            {
                if (string.IsNullOrWhiteSpace(actorId) || modifier == null || string.IsNullOrWhiteSpace(modifier.SourceId) || string.IsNullOrWhiteSpace(modifier.ModifierId))
                {
                    return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, "Modifier 参数无效。", actorId, _revision);
                }

                if (!string.IsNullOrWhiteSpace(FailOnModifierId)
                    && string.Equals(modifier.ModifierId, FailOnModifierId, StringComparison.Ordinal))
                {
                    return AttributeOperationResult.Failed(AttributeFailureReason.InvalidModifier, "测试用强制失败。", actorId, _revision);
                }

                _modifiers[BuildKey(actorId, modifier.SourceId, modifier.ModifierId)] = modifier.Clone();
                return AttributeOperationResult.Success(actorId, ++_revision);
            }

            public AttributeOperationResult RemoveModifier(string actorId, string sourceId, string modifierId)
            {
                _modifiers.Remove(BuildKey(actorId, sourceId, modifierId));
                return AttributeOperationResult.Success(actorId, ++_revision);
            }

            public AttributeOperationResult RemoveModifiersBySource(string actorId, string sourceId)
            {
                RemoveBySourceCallCount++;
                var keys = new List<string>();
                foreach (var pair in _modifiers)
                {
                    if (pair.Value != null
                        && string.Equals(pair.Value.SourceId, sourceId, StringComparison.Ordinal)
                        && pair.Key.StartsWith(actorId + "|", StringComparison.Ordinal))
                    {
                        keys.Add(pair.Key);
                    }
                }

                for (var i = 0; i < keys.Count; i++)
                {
                    _modifiers.Remove(keys[i]);
                }

                return AttributeOperationResult.Success(actorId, ++_revision);
            }

            public AttributeOperationResult ConsumeResource(string actorId, string resourceId, float amount, string sourceModule = null)
            {
                return AttributeOperationResult.Success(resourceId, ++_revision);
            }

            public AttributeOperationResult RecoverResource(string actorId, string resourceId, float amount, string sourceModule = null)
            {
                return AttributeOperationResult.Success(resourceId, ++_revision);
            }

            public AttributeOperationResult SetResourceCurrent(string actorId, string resourceId, float value, string sourceModule = null)
            {
                return AttributeOperationResult.Success(resourceId, ++_revision);
            }

            public bool TryGetModifier(string actorId, string sourceId, string modifierId, out AttributeModifier modifier)
            {
                if (_modifiers.TryGetValue(BuildKey(actorId, sourceId, modifierId), out var found))
                {
                    modifier = found.Clone();
                    return true;
                }

                modifier = null;
                return false;
            }

            public bool HasSource(string actorId, string sourceId)
            {
                return CountBySource(actorId, sourceId) > 0;
            }

            public int CountBySource(string actorId, string sourceId)
            {
                var count = 0;
                foreach (var pair in _modifiers)
                {
                    if (pair.Value != null
                        && string.Equals(pair.Value.SourceId, sourceId, StringComparison.Ordinal)
                        && pair.Key.StartsWith(actorId + "|", StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                return count;
            }

            private static string BuildKey(string actorId, string sourceId, string modifierId)
            {
                return (actorId ?? string.Empty) + "|" + (sourceId ?? string.Empty) + "|" + (modifierId ?? string.Empty);
            }
        }
    }
}
