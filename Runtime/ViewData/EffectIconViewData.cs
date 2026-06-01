using System;
using NiumaEffect.Enum;

namespace NiumaEffect.ViewData
{
    /// <summary>
    /// 单个效果图标表现数据。
    /// UI 只读该结构，不应反向修改效果运行时状态。
    /// </summary>
    [Serializable]
    public sealed class EffectIconViewData
    {
        public string EffectInstanceId;
        public string EffectId;
        public string DisplayName;
        public string Description;
        public string IconAddress;

        public EffectType Type = EffectType.None;
        public EffectPolarity Polarity = EffectPolarity.Neutral;

        public int StackCount;
        public float DurationSeconds;
        public float RemainingSeconds;
        public float Progress01;

        public bool IsPermanent;
        public bool IsDispellable;
        public bool IsMissingDefinition;

        public string[] Tags = Array.Empty<string>();

        public EffectIconViewData Clone()
        {
            return new EffectIconViewData
            {
                EffectInstanceId = EffectInstanceId,
                EffectId = EffectId,
                DisplayName = DisplayName,
                Description = Description,
                IconAddress = IconAddress,
                Type = Type,
                Polarity = Polarity,
                StackCount = StackCount,
                DurationSeconds = DurationSeconds,
                RemainingSeconds = RemainingSeconds,
                Progress01 = Progress01,
                IsPermanent = IsPermanent,
                IsDispellable = IsDispellable,
                IsMissingDefinition = IsMissingDefinition,
                Tags = Tags == null || Tags.Length == 0 ? Array.Empty<string>() : (string[])Tags.Clone()
            };
        }

        public static EffectIconViewData[] CloneArray(EffectIconViewData[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<EffectIconViewData>();
            }

            var result = new EffectIconViewData[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }
}
