namespace NiumaEffect.Enum
{
    /// <summary>
    /// 效果施加失败原因。
    /// UI 不应匹配 Message 字符串，而应根据该枚举做本地化提示。
    /// </summary>
    public enum EffectApplyFailureReason
    {
        /// <summary>没有失败。</summary>
        None = 0,

        /// <summary>请求参数无效。</summary>
        InvalidRequest = 1,

        /// <summary>效果配置不存在。</summary>
        DefinitionMissing = 2,

        /// <summary>目标 Actor 不存在或为空。</summary>
        OwnerActorMissing = 3,

        /// <summary>外部施加条件拒绝。</summary>
        ApplicationBlocked = 4,

        /// <summary>目标免疫该效果。</summary>
        Immune = 5,

        /// <summary>叠层规则拒绝重复添加。</summary>
        AlreadyExists = 6,

        /// <summary>需要写入属性修饰器，但属性服务未就绪。</summary>
        AttributeServiceMissing = 7,

        /// <summary>属性修饰器写入失败。</summary>
        ModifierApplyFailed = 8,

        /// <summary>叠层配置无效。</summary>
        StackRuleInvalid = 9
    }
}
