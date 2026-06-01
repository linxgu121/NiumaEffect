using System;
using NiumaEffect.Enum;

namespace NiumaEffect.Request
{
    /// <summary>
    /// 驱散效果请求。
    /// 第一版只提供基础筛选条件，不做复杂驱散优先级公式。
    /// </summary>
    [Serializable]
    public sealed class EffectDispelRequest
    {
        /// <summary>要驱散的目标 ActorId。</summary>
        public string ActorId;

        /// <summary>目标正负性。None 语义由服务层决定，建议表示不限制。</summary>
        public EffectPolarity TargetPolarity = EffectPolarity.Harmful;

        /// <summary>必须命中的标签。为空表示不按标签过滤。</summary>
        public string[] RequiredTags = Array.Empty<string>();

        /// <summary>最多驱散数量。小于等于 0 表示不限制。</summary>
        public int MaxCount;

        /// <summary>驱散来源 ActorId。</summary>
        public string SourceActorId;

        /// <summary>驱散来源模块名。</summary>
        public string SourceModule;

        public EffectDispelRequest Clone()
        {
            var tags = RequiredTags == null || RequiredTags.Length == 0
                ? Array.Empty<string>()
                : (string[])RequiredTags.Clone();

            return new EffectDispelRequest
            {
                ActorId = ActorId,
                TargetPolarity = TargetPolarity,
                RequiredTags = tags,
                MaxCount = MaxCount,
                SourceActorId = SourceActorId,
                SourceModule = SourceModule
            };
        }
    }
}
