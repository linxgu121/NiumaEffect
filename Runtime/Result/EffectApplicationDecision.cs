using NiumaEffect.Enum;

namespace NiumaEffect.Result
{
    /// <summary>
    /// 外部施加条件或免疫解析器的裁决结果。
    /// Message 只用于调试，正式 UI 不应依赖字符串匹配。
    /// </summary>
    public readonly struct EffectApplicationDecision
    {
        public readonly bool Allowed;
        public readonly EffectApplyFailureReason FailureReason;
        public readonly string Message;

        public static EffectApplicationDecision Allow()
        {
            return new EffectApplicationDecision(true, EffectApplyFailureReason.None, null);
        }

        public static EffectApplicationDecision Deny(EffectApplyFailureReason reason, string message = null)
        {
            return new EffectApplicationDecision(false, reason, message);
        }

        private EffectApplicationDecision(bool allowed, EffectApplyFailureReason failureReason, string message)
        {
            Allowed = allowed;
            FailureReason = failureReason;
            Message = message;
        }
    }
}
