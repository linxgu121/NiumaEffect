using NiumaEffect.Request;
using NiumaEffect.Result;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 效果免疫解析器。
    /// 用于判断目标是否免疫某个效果，例如 Boss 免疫眩晕、剧情保护免疫 Debuff。
    /// </summary>
    public interface IEffectImmunityResolver
    {
        EffectApplicationDecision CanApply(in EffectApplicationContext context);
    }
}
