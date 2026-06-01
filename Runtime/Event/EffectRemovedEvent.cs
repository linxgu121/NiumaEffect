using NiumaEffect.Enum;

namespace NiumaEffect.Event
{
    /// <summary>
    /// 效果被移除事件。
    /// </summary>
    public readonly struct EffectRemovedEvent
    {
        public readonly string ActorId;
        public readonly string EffectInstanceId;
        public readonly string EffectId;
        public readonly EffectRemoveReason Reason;

        public EffectRemovedEvent(
            string actorId,
            string effectInstanceId,
            string effectId,
            EffectRemoveReason reason)
        {
            ActorId = actorId;
            EffectInstanceId = effectInstanceId;
            EffectId = effectId;
            Reason = reason;
        }
    }
}
