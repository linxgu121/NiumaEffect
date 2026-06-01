namespace NiumaEffect.Enum
{
    /// <summary>
    /// 效果正负性。
    /// 主要用于驱散、净化和 UI 分类，不等同于 EffectType。
    /// </summary>
    public enum EffectPolarity
    {
        /// <summary>中性效果。</summary>
        Neutral = 0,

        /// <summary>正面效果。</summary>
        Beneficial = 1,

        /// <summary>负面效果。</summary>
        Harmful = 2
    }
}
