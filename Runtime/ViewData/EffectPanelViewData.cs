using System;

namespace NiumaEffect.ViewData
{
    /// <summary>
    /// 某个 Actor 的效果面板表现数据。
    /// </summary>
    [Serializable]
    public sealed class EffectPanelViewData
    {
        public string ActorId;
        public long Revision;
        public EffectIconViewData[] Effects = Array.Empty<EffectIconViewData>();

        public EffectPanelViewData Clone()
        {
            return new EffectPanelViewData
            {
                ActorId = ActorId,
                Revision = Revision,
                Effects = EffectIconViewData.CloneArray(Effects)
            };
        }
    }
}
