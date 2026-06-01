using System;

namespace NiumaEffect.Data
{
    /// <summary>
    /// 效果轻量扩展数据。
    /// 不使用 Dictionary，保证 Unity JsonUtility 和存档结构稳定。
    /// </summary>
    [Serializable]
    public sealed class EffectCustomDataEntry
    {
        /// <summary>扩展键。建议小写下划线，并加模块前缀。</summary>
        public string Key;

        /// <summary>扩展值。只存轻量字符串，不存大 JSON 或资源列表。</summary>
        public string Value;

        public EffectCustomDataEntry Clone()
        {
            return new EffectCustomDataEntry
            {
                Key = Key,
                Value = Value
            };
        }

        public static EffectCustomDataEntry[] CloneArray(EffectCustomDataEntry[] source)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<EffectCustomDataEntry>();
            }

            var result = new EffectCustomDataEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                result[i] = source[i]?.Clone();
            }

            return result;
        }
    }
}
