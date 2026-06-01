using System;
using NiumaEffect.Enum;

namespace NiumaEffect.Data
{
    /// <summary>
    /// 运行中效果的存档 / 查询快照。
    /// 快照是纯数据 DTO，不暴露运行时可变引用。
    /// </summary>
    [Serializable]
    public sealed class ActiveEffectSnapshot
    {
        public string EffectInstanceId;
        public string EffectId;
        public string OwnerActorId;
        public string SourceActorId;
        public string SourceModule;

        public float DurationSeconds;
        public float RemainingSeconds;
        public int StackCount;

        public EffectType Type = EffectType.None;
        public EffectPolarity Polarity = EffectPolarity.Neutral;
        public bool IsDispellable;

        public bool IsMissingDefinition;
        public bool IsPendingModifierApply;

        public string[] Tags = Array.Empty<string>();
        public EffectCustomDataEntry[] CustomData = Array.Empty<EffectCustomDataEntry>();

        public ActiveEffectSnapshot Clone()
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
                Tags = CloneStringArray(Tags),
                CustomData = EffectCustomDataEntry.CloneArray(CustomData)
            };
        }

        public static ActiveEffectSnapshot[] CloneArray(ActiveEffectSnapshot[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<ActiveEffectSnapshot>();
            }

            var result = new ActiveEffectSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }

        internal static string[] CloneStringArray(string[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Length];
            Array.Copy(source, result, source.Length);
            return result;
        }
    }
}
