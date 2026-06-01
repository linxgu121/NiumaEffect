namespace NiumaEffect.Enum
{
    /// <summary>
    /// 效果 UI 更新类型。
    /// 第一版以全量刷新为主，后续可扩展增量更新。
    /// </summary>
    public enum EffectUIUpdateType
    {
        /// <summary>全量刷新。</summary>
        Refresh = 0,

        /// <summary>清空显示。</summary>
        Cleared = 1
    }
}
