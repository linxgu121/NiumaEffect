using NiumaEffect.Request;
using NiumaEffect.Result;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 外部施加条件解析器。
    /// 用于判断死亡状态、剧情保护、安全区、技能命中等 Effect 模块外部条件。
    /// </summary>
    public interface IEffectApplicationResolver
    {
        EffectApplicationDecision CanApply(in EffectApplicationContext context);
    }
}
