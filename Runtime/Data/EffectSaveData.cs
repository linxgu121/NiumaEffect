using System;

namespace NiumaEffect.Data
{
    /// <summary>
    /// 效果模块存档数据。
    /// 保存全部运行中效果，但不保存这些效果生成的 AttributeModifier。
    /// </summary>
    [Serializable]
    public sealed class EffectSaveData
    {
        public int Version = 1;
        public long Revision;
        public EffectOwnerSnapshot[] Owners = Array.Empty<EffectOwnerSnapshot>();

        public EffectSaveData Clone()
        {
            return new EffectSaveData
            {
                Version = Version,
                Revision = Revision,
                Owners = EffectOwnerSnapshot.CloneArray(Owners)
            };
        }
    }
}
