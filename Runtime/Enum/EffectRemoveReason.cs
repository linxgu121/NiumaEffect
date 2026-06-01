namespace NiumaEffect.Enum
{
    /// <summary>
    /// 效果被移除的原因。
    /// 用于日志、调试、UI 提示和后续事件通知。
    /// </summary>
    public enum EffectRemoveReason
    {
        /// <summary>未设置。</summary>
        None = 0,

        /// <summary>持续时间结束。</summary>
        Expired = 1,

        /// <summary>被普通驱散移除。</summary>
        Dispelled = 2,

        /// <summary>被净化移除。</summary>
        Cleansed = 3,

        /// <summary>被新效果替换。</summary>
        Replaced = 4,

        /// <summary>来源被清理，例如技能、装备或剧情来源失效。</summary>
        SourceRemoved = 5,

        /// <summary>拥有者被移除。</summary>
        OwnerRemoved = 6,

        /// <summary>外部手动移除。</summary>
        Manual = 7,

        /// <summary>导入或迁移时发现无效数据。</summary>
        ImportInvalid = 8
    }
}
