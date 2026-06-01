namespace NiumaEffect.Enum
{
    /// <summary>
    /// 效果持续模式。
    /// </summary>
    public enum EffectDurationMode
    {
        /// <summary>瞬时效果。第一版只预留，不做完整执行器。</summary>
        Instant = 0,

        /// <summary>持续一段时间，到期后自动移除。</summary>
        Duration = 1,

        /// <summary>永久效果，通常由存档或剧情控制移除。</summary>
        Permanent = 2,

        /// <summary>直到外部显式移除。</summary>
        UntilRemoved = 3
    }
}
