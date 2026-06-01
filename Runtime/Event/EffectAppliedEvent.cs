namespace NiumaEffect.Event
{
    /// <summary>
    /// 效果成功施加事件。
    /// 事件只做通知，不作为效果状态一致性的依赖。
    /// </summary>
    public readonly struct EffectAppliedEvent
    {
        public readonly string ActorId;
        public readonly string EffectInstanceId;
        public readonly string EffectId;
        public readonly string SourceActorId;
        public readonly string SourceModule;
        public readonly int StackCount;

        public EffectAppliedEvent(
            string actorId,
            string effectInstanceId,
            string effectId,
            string sourceActorId,
            string sourceModule,
            int stackCount)
        {
            ActorId = actorId;
            EffectInstanceId = effectInstanceId;
            EffectId = effectId;
            SourceActorId = sourceActorId;
            SourceModule = sourceModule;
            StackCount = stackCount;
        }
    }
}
