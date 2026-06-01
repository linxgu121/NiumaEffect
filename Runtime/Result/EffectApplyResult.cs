using System;
using NiumaEffect.Data;
using NiumaEffect.Enum;

namespace NiumaEffect.Result
{
    /// <summary>
    /// 施加效果结果。
    /// 所有失败都应通过 FailureReason 结构化表达，Message 只用于调试。
    /// </summary>
    [Serializable]
    public sealed class EffectApplyResult
    {
        /// <summary>施加是否成功。</summary>
        public bool Succeeded;

        /// <summary>失败原因。成功时为 None。</summary>
        public EffectApplyFailureReason FailureReason = EffectApplyFailureReason.None;

        /// <summary>调试信息或临时提示，正式本地化不要依赖该字段。</summary>
        public string Message;

        /// <summary>目标 ActorId。</summary>
        public string ActorId;

        /// <summary>效果 ID。</summary>
        public string EffectId;

        /// <summary>效果实例 ID。失败时可以为空。</summary>
        public string EffectInstanceId;

        /// <summary>施加或更新后的效果快照。</summary>
        public ActiveEffectSnapshot Effect;

        /// <summary>是否创建了新实例。刷新或叠层旧实例时为 false。</summary>
        public bool CreatedNewInstance;

        /// <summary>是否改变了层数。</summary>
        public bool StackChanged;

        public static EffectApplyResult Success(
            string actorId,
            string effectId,
            string effectInstanceId,
            ActiveEffectSnapshot effect,
            bool createdNewInstance,
            bool stackChanged,
            string message = null)
        {
            return new EffectApplyResult
            {
                Succeeded = true,
                FailureReason = EffectApplyFailureReason.None,
                Message = message,
                ActorId = actorId,
                EffectId = effectId,
                EffectInstanceId = effectInstanceId,
                Effect = effect?.Clone(),
                CreatedNewInstance = createdNewInstance,
                StackChanged = stackChanged
            };
        }

        public static EffectApplyResult Failed(
            EffectApplyFailureReason reason,
            string actorId = null,
            string effectId = null,
            string message = null)
        {
            return new EffectApplyResult
            {
                Succeeded = false,
                FailureReason = reason,
                Message = message,
                ActorId = actorId,
                EffectId = effectId,
                EffectInstanceId = null,
                Effect = null,
                CreatedNewInstance = false,
                StackChanged = false
            };
        }
    }
}
