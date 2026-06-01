using NiumaEffect.Config;
using NiumaEffect.Data;

namespace NiumaEffect.Request
{
    /// <summary>
    /// 效果施加裁决上下文。
    /// Resolver 只能读取该结构，不应修改运行时状态。
    /// </summary>
    public readonly struct EffectApplicationContext
    {
        public readonly EffectApplyRequest Request;
        public readonly EffectDefinition Definition;
        public readonly ActiveEffectSnapshot[] ExistingEffects;
        public readonly int ExistingStackCount;
        public readonly bool HasSameEffect;

        public EffectApplicationContext(
            EffectApplyRequest request,
            EffectDefinition definition,
            ActiveEffectSnapshot[] existingEffects,
            int existingStackCount,
            bool hasSameEffect)
        {
            Request = request;
            Definition = definition;
            ExistingEffects = existingEffects ?? System.Array.Empty<ActiveEffectSnapshot>();
            ExistingStackCount = existingStackCount;
            HasSameEffect = hasSameEffect;
        }
    }
}
