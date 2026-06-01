using NiumaEffect.Enum;

namespace NiumaEffect.ViewData
{
    /// <summary>
    /// 效果 UI 更新包。
    /// Previous 用于 UI 自己做 diff、动画或数值变化提示。
    /// </summary>
    public readonly struct EffectUIUpdate
    {
        public readonly EffectUIUpdateType UpdateType;
        public readonly long Revision;
        public readonly EffectPanelViewData Current;
        public readonly EffectPanelViewData Previous;

        public EffectUIUpdate(
            EffectUIUpdateType updateType,
            long revision,
            EffectPanelViewData current,
            EffectPanelViewData previous)
        {
            UpdateType = updateType;
            Revision = revision;
            Current = current;
            Previous = previous;
        }
    }
}
