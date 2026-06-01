using System;
using NiumaEffect.Enum;

namespace NiumaEffect.Data
{
    /// <summary>
    /// 效果运行时状态。
    /// 服务层内部使用该对象维护剩余时间、层数、缺失配置和待应用 Modifier 标记。
    /// </summary>
    [Serializable]
    public sealed class ActiveEffectRuntimeState
    {
        public string EffectInstanceId;
        public string EffectId;
        public string OwnerActorId;
        public string SourceActorId;
        public string SourceModule;

        public EffectType Type = EffectType.None;
        public EffectPolarity Polarity = EffectPolarity.Neutral;

        public float DurationSeconds;
        public float RemainingSeconds;
        public int StackCount = 1;

        public bool IsDispellable;
        public bool IsMissingDefinition;
        public bool IsPendingModifierApply;

        public string[] Tags = Array.Empty<string>();
        public EffectCustomDataEntry[] CustomData = Array.Empty<EffectCustomDataEntry>();

        /// <summary>
        /// 根据效果实例 ID 推导 AttributeModifier.SourceId。
        /// </summary>
        public string ModifierSourceId => EffectProtocolUtility.BuildModifierSourceId(EffectInstanceId);

        public ActiveEffectSnapshot ToSnapshot()
        {
            return new ActiveEffectSnapshot
            {
                EffectInstanceId = EffectInstanceId,
                EffectId = EffectId,
                OwnerActorId = OwnerActorId,
                SourceActorId = SourceActorId,
                SourceModule = SourceModule,
                DurationSeconds = DurationSeconds,
                RemainingSeconds = RemainingSeconds,
                StackCount = StackCount,
                Type = Type,
                Polarity = Polarity,
                IsDispellable = IsDispellable,
                IsMissingDefinition = IsMissingDefinition,
                IsPendingModifierApply = IsPendingModifierApply,
                Tags = ActiveEffectSnapshot.CloneStringArray(Tags),
                CustomData = EffectCustomDataEntry.CloneArray(CustomData)
            };
        }

        public static ActiveEffectRuntimeState FromSnapshot(ActiveEffectSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new ActiveEffectRuntimeState
            {
                EffectInstanceId = snapshot.EffectInstanceId,
                EffectId = snapshot.EffectId,
                OwnerActorId = snapshot.OwnerActorId,
                SourceActorId = snapshot.SourceActorId,
                SourceModule = snapshot.SourceModule,
                DurationSeconds = snapshot.DurationSeconds,
                RemainingSeconds = snapshot.RemainingSeconds,
                StackCount = Math.Max(1, snapshot.StackCount),
                Type = snapshot.Type,
                Polarity = snapshot.Polarity,
                IsDispellable = snapshot.IsDispellable,
                IsMissingDefinition = snapshot.IsMissingDefinition,
                IsPendingModifierApply = snapshot.IsPendingModifierApply,
                Tags = ActiveEffectSnapshot.CloneStringArray(snapshot.Tags),
                CustomData = EffectCustomDataEntry.CloneArray(snapshot.CustomData)
            };
        }
    }
}
