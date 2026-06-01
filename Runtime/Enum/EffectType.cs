namespace NiumaEffect.Enum
{
    /// <summary>
    /// 效果业务类型。
    /// None 只用于默认值保护，正式效果不要使用 None。
    /// </summary>
    public enum EffectType
    {
        /// <summary>未设置。运行时传入该值视为非法。</summary>
        None = 0,

        /// <summary>正面增益效果。</summary>
        Buff = 1,

        /// <summary>负面减益效果。</summary>
        Debuff = 2,

        /// <summary>中性状态，例如潜行、冥想。</summary>
        State = 3,

        /// <summary>控制效果，例如眩晕、沉默、禁足。</summary>
        Control = 4,

        /// <summary>环境效果，例如寒冷、潮湿。</summary>
        Environment = 5,

        /// <summary>剧情效果，例如氏族祝福、祖训加持。</summary>
        Story = 6
    }
}
