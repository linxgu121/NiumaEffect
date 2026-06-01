using System;
using NiumaEffect.Enum;

namespace NiumaEffect.Config
{
    /// <summary>
    /// 效果叠层规则配置。
    /// </summary>
    [Serializable]
    public sealed class EffectStackRuleData
    {
        [UnityEngine.Tooltip("同类效果重复施加时的叠层模式。")]
        public EffectStackMode Mode = EffectStackMode.RefreshDuration;

        [UnityEngine.Tooltip("最大层数。小于 1 时服务层会按 1 处理。")]
        public int MaxStack = 1;

        [UnityEngine.Tooltip("叠层后是否按层数放大该效果生成的属性修饰器数值。")]
        public bool ScaleModifiersByStack = true;

        public EffectStackRuleData Clone()
        {
            return new EffectStackRuleData
            {
                Mode = Mode,
                MaxStack = Math.Max(1, MaxStack),
                ScaleModifiersByStack = ScaleModifiersByStack
            };
        }
    }
}
