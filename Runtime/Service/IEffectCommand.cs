using NiumaEffect.Enum;
using NiumaEffect.Request;
using NiumaEffect.Result;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 效果命令接口。
    /// 所有方法都可能修改运行中效果状态。
    /// </summary>
    public interface IEffectCommand
    {
        EffectApplyResult ApplyEffect(EffectApplyRequest request);
        EffectOperationResult RemoveEffect(string actorId, string effectInstanceId, EffectRemoveReason reason);
        EffectOperationResult RemoveEffectsBySource(string actorId, string sourceActorId, string sourceModule = null);
        EffectOperationResult DispelEffects(EffectDispelRequest request);
        EffectOperationResult ClearEffects(string actorId, EffectRemoveReason reason);

        /// <summary>
        /// 驱动效果生命周期。
        /// 持续时间递减本身不应导致 Revision 递增，只有过期移除才递增。
        /// </summary>
        void Tick(float deltaTime);
    }
}
