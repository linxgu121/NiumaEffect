using System;
using UnityEngine;

namespace NiumaEffect.Config
{
    /// <summary>
    /// 效果标签公共清单。
    /// 用于约束策划和程序使用同一批标签，避免大小写和命名风格混乱。
    /// </summary>
    [CreateAssetMenu(menuName = "NiumaEffect/Effect Tag Catalog", fileName = "EffectTagCatalog")]
    public sealed class EffectTagCatalog : ScriptableObject
    {
        [Tooltip("效果标签清单。统一使用小写下划线，例如 buff_attack、debuff_slow、control。")]
        public string[] Tags = Array.Empty<string>();
    }
}
