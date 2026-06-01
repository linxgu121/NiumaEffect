using System;
using NiumaEffect.Enum;
using UnityEngine;

namespace NiumaEffect.Config
{
    /// <summary>
    /// 效果配置资产。
    /// 静态配置由策划维护，不保存运行时剩余时间和叠层。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaEffect/Effect Definition", fileName = "EffectDefinition")]
    public sealed class EffectDefinition : ScriptableObject
    {
        [Tooltip("效果稳定 ID。用于存档、任务、技能和调试。一旦发布不要修改。")]
        public string EffectId;

        [Tooltip("效果显示名称。后续接本地化时可以改成本地化 Key。")]
        public string DisplayName;

        [Tooltip("效果说明。用于 UI、调试或策划查看。")]
        [TextArea]
        public string Description;

        [Tooltip("图标 Addressables Key 或资源路径。")]
        public string IconAddress;

        [Tooltip("效果业务类型。None 仅用于默认值保护，正式效果不要使用 None。")]
        public EffectType Type = EffectType.Buff;

        [Tooltip("效果正负性。用于驱散和 UI 分类。")]
        public EffectPolarity Polarity = EffectPolarity.Beneficial;

        [Tooltip("持续模式。第一版主要支持 Duration / Permanent / UntilRemoved。")]
        public EffectDurationMode DurationMode = EffectDurationMode.Duration;

        [Tooltip("持续时间。Duration 模式下必须大于 0。")]
        public float DurationSeconds = 5f;

        [Tooltip("是否可被普通驱散移除。")]
        public bool IsDispellable = true;

        [Tooltip("效果标签。统一使用小写下划线，并建议登记在 EffectTagCatalog。")]
        public string[] Tags = Array.Empty<string>();

        [Tooltip("叠层规则。为空时服务层应使用 RefreshDuration + 1 层的默认规则。")]
        public EffectStackRuleData StackRule = new EffectStackRuleData();

        [Tooltip("该效果产生的属性修饰器模板。没有模板时效果只记录生命周期和 UI 状态。")]
        public EffectModifierTemplateData[] ModifierTemplates = Array.Empty<EffectModifierTemplateData>();
    }
}
