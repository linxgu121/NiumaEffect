using System;

namespace NiumaEffect.Data
{
    /// <summary>
    /// 单个 Actor 的效果快照。
    /// </summary>
    [Serializable]
    public sealed class EffectOwnerSnapshot
    {
        public string ActorId;
        public ActiveEffectSnapshot[] Effects = Array.Empty<ActiveEffectSnapshot>();

        public EffectOwnerSnapshot Clone()
        {
            return new EffectOwnerSnapshot
            {
                ActorId = ActorId,
                Effects = ActiveEffectSnapshot.CloneArray(Effects)
            };
        }

        public static EffectOwnerSnapshot[] CloneArray(EffectOwnerSnapshot[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<EffectOwnerSnapshot>();
            }

            var result = new EffectOwnerSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }
}
