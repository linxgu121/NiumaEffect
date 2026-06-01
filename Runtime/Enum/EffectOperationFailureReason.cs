namespace NiumaEffect.Enum
{
    /// <summary>
    /// 通用效果操作失败原因。
    /// 用于移除、驱散、导入等非 ApplyEffect 操作。
    /// </summary>
    public enum EffectOperationFailureReason
    {
        /// <summary>没有失败。</summary>
        None = 0,

        /// <summary>请求参数无效。</summary>
        InvalidRequest = 1,

        /// <summary>目标效果不存在。</summary>
        EffectNotFound = 2,

        /// <summary>目标 Actor 不存在或为空。</summary>
        OwnerActorMissing = 3,

        /// <summary>效果不可驱散。</summary>
        NotDispellable = 4,

        /// <summary>属性修饰器清理失败。</summary>
        ModifierCleanupFailed = 5,

        /// <summary>导入数据无效。</summary>
        ImportInvalid = 6,

        /// <summary>服务尚未就绪。</summary>
        ServiceNotReady = 7
    }
}
