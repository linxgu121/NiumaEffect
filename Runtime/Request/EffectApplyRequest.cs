using System;
using NiumaEffect.Data;

namespace NiumaEffect.Request
{
    /// <summary>
    /// 外部模块施加效果的请求。
    /// 技能、剧情、交互、装备等模块都应通过该 DTO 请求效果模块。
    /// </summary>
    [Serializable]
    public sealed class EffectApplyRequest
    {
        /// <summary>要施加的效果配置 ID。</summary>
        public string EffectId;

        /// <summary>被施加效果的目标 ActorId。</summary>
        public string OwnerActorId;

        /// <summary>施加者 ActorId。环境、剧情、陷阱等来源可以为空。</summary>
        public string SourceActorId;

        /// <summary>来源模块名，例如 NiumaSkill、NiumaStory、NiumaInteract。</summary>
        public string SourceModule;

        /// <summary>本次施加的层数。服务层会把小于 1 的值按 1 处理。</summary>
        public int StackCount = 1;

        /// <summary>持续时间覆盖值。大于 0 时覆盖配置持续时间，小于等于 0 时使用配置值。</summary>
        public float DurationOverrideSeconds = -1f;

        /// <summary>是否忽略外部施加条件解析器。仅建议调试或强制剧情流程使用。</summary>
        public bool IgnoreApplicationResolver;

        /// <summary>是否忽略免疫解析器。仅建议调试或强制剧情流程使用。</summary>
        public bool IgnoreImmunityResolver;

        /// <summary>轻量扩展数据，不使用 Dictionary。</summary>
        public EffectCustomDataEntry[] CustomData = Array.Empty<EffectCustomDataEntry>();

        public EffectApplyRequest Clone()
        {
            return new EffectApplyRequest
            {
                EffectId = EffectId,
                OwnerActorId = OwnerActorId,
                SourceActorId = SourceActorId,
                SourceModule = SourceModule,
                StackCount = StackCount,
                DurationOverrideSeconds = DurationOverrideSeconds,
                IgnoreApplicationResolver = IgnoreApplicationResolver,
                IgnoreImmunityResolver = IgnoreImmunityResolver,
                CustomData = EffectCustomDataEntry.CloneArray(CustomData)
            };
        }
    }
}
