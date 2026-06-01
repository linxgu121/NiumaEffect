namespace NiumaEffect.ViewData
{
    /// <summary>
    /// 效果 UI 接收端。
    /// 具体 UI 预制体由 UI 模块或 UI 策划制作，效果模块只推送表现数据。
    /// </summary>
    public interface IEffectUIReceiver
    {
        void ApplyEffectUpdate(EffectUIUpdate update);
    }
}
