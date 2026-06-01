namespace NiumaEffect.Enum
{
    /// <summary>
    /// 同类效果重复施加时的叠层策略。
    /// </summary>
    public enum EffectStackMode
    {
        /// <summary>已有同类效果时拒绝新效果。</summary>
        Reject = 0,

        /// <summary>不增加层数，只刷新剩余时间。</summary>
        RefreshDuration = 1,

        /// <summary>增加层数并刷新剩余时间。</summary>
        AddStackRefreshDuration = 2,

        /// <summary>增加层数但保留原剩余时间。</summary>
        AddStackKeepDuration = 3,

        /// <summary>移除旧效果并添加新效果。</summary>
        ReplaceOld = 4,

        /// <summary>每次施加都创建独立实例。</summary>
        IndependentInstance = 5
    }
}
