namespace NiumaEffect.Event
{
    /// <summary>
    /// 效果层数变化事件。
    /// </summary>
    public readonly struct EffectStackChangedEvent
    {
        public readonly string ActorId;
        public readonly string EffectInstanceId;
        public readonly string EffectId;
        public readonly int OldStackCount;
        public readonly int NewStackCount;

        public EffectStackChangedEvent(
            string actorId,
            string effectInstanceId,
            string effectId,
            int oldStackCount,
            int newStackCount)
        {
            ActorId = actorId;
            EffectInstanceId = effectInstanceId;
            EffectId = effectId;
            OldStackCount = oldStackCount;
            NewStackCount = newStackCount;
        }
    }
}
