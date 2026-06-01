using System;

namespace NiumaEffect.Data
{
    /// <summary>
    /// 效果协议工具。
    /// 只提供稳定 ID 与协议字符串生成，不包含业务裁决逻辑。
    /// </summary>
    public static class EffectProtocolUtility
    {
        public const string EffectInstancePrefix = "effect_";
        public const string ModifierSourcePrefix = "effect:";

        /// <summary>
        /// 创建新的效果实例 ID。
        /// </summary>
        public static string CreateEffectInstanceId()
        {
            return EffectInstancePrefix + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// 根据效果实例 ID 推导 AttributeModifier.SourceId。
        /// </summary>
        public static string BuildModifierSourceId(string effectInstanceId)
        {
            return string.IsNullOrWhiteSpace(effectInstanceId)
                ? null
                : ModifierSourcePrefix + effectInstanceId;
        }
    }
}
