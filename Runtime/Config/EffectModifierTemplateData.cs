using System;
using NiumaAttribute.Data;
using NiumaAttribute.Enum;
using NiumaEffect.Data;

namespace NiumaEffect.Config
{
    /// <summary>
    /// 效果生成属性修饰器的模板。
    /// 真正的 AttributeModifier 在效果实例创建或叠层变化时由服务层生成。
    /// </summary>
    [Serializable]
    public sealed class EffectModifierTemplateData
    {
        [UnityEngine.Tooltip("模板内唯一 ID。同一个效果配置内不要重复。")]
        public string ModifierId;

        [UnityEngine.Tooltip("目标属性 ID，例如 attack、defense、move_speed。")]
        public string TargetAttributeId;

        [UnityEngine.Tooltip("属性修饰器运算类型。")]
        public AttributeModifierOperation Operation = AttributeModifierOperation.None;

        [UnityEngine.Tooltip("属性修饰器计算分层。效果模块通常使用 Effect 层。")]
        public AttributeModifierLayer Layer = AttributeModifierLayer.Effect;

        [UnityEngine.Tooltip("修饰器基础值。")]
        public float Value;

        [UnityEngine.Tooltip("同层排序优先级，数值越小越先计算。")]
        public int Priority;

        public EffectModifierTemplateData Clone()
        {
            return new EffectModifierTemplateData
            {
                ModifierId = ModifierId,
                TargetAttributeId = TargetAttributeId,
                Operation = Operation,
                Layer = Layer,
                Value = Value,
                Priority = Priority
            };
        }

        /// <summary>
        /// 根据效果实例生成 AttributeModifier。
        /// 生成的 Modifier 生命周期由 NiumaEffect 管理，因此 DurationSeconds / RemainingSeconds 固定为 0。
        /// </summary>
        public AttributeModifier BuildModifier(string effectInstanceId, int stackCount, bool scaleByStack)
        {
            var safeStack = Math.Max(1, stackCount);
            return new AttributeModifier
            {
                SourceId = EffectProtocolUtility.BuildModifierSourceId(effectInstanceId),
                ModifierId = ModifierId,
                TargetAttributeId = TargetAttributeId,
                Operation = Operation,
                Layer = Layer,
                Value = scaleByStack ? Value * safeStack : Value,
                Priority = Priority,
                IsPersistent = false,
                DurationSeconds = 0f,
                RemainingSeconds = 0f,
                SourceModule = "NiumaEffect"
            };
        }
    }
}
