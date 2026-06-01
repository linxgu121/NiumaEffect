using System;
using NiumaEffect.Data;
using NiumaEffect.Enum;

namespace NiumaEffect.Result
{
    /// <summary>
    /// 通用效果操作结果。
    /// 用于移除、驱散、清空和导入等非 ApplyEffect 操作。
    /// </summary>
    [Serializable]
    public sealed class EffectOperationResult
    {
        /// <summary>操作是否成功。</summary>
        public bool Succeeded;

        /// <summary>失败原因。成功时为 None。</summary>
        public EffectOperationFailureReason FailureReason = EffectOperationFailureReason.None;

        /// <summary>调试信息或临时提示，正式本地化不要依赖该字段。</summary>
        public string Message;

        /// <summary>受影响 ActorId。</summary>
        public string ActorId;

        /// <summary>受影响效果。</summary>
        public ActiveEffectSnapshot[] ChangedEffects = Array.Empty<ActiveEffectSnapshot>();

        public static EffectOperationResult Success(
            string actorId = null,
            ActiveEffectSnapshot[] changedEffects = null,
            string message = null)
        {
            return new EffectOperationResult
            {
                Succeeded = true,
                FailureReason = EffectOperationFailureReason.None,
                Message = message,
                ActorId = actorId,
                ChangedEffects = ActiveEffectSnapshot.CloneArray(changedEffects)
            };
        }

        public static EffectOperationResult Failed(
            EffectOperationFailureReason reason,
            string actorId = null,
            string message = null,
            ActiveEffectSnapshot[] changedEffects = null)
        {
            return new EffectOperationResult
            {
                Succeeded = false,
                FailureReason = reason,
                Message = message,
                ActorId = actorId,
                ChangedEffects = ActiveEffectSnapshot.CloneArray(changedEffects)
            };
        }
    }
}
