using NiumaEffect.Data;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 效果查询接口。
    /// 查询返回快照，不暴露运行时可变引用。
    /// </summary>
    public interface IEffectQuery
    {
        /// <summary>效果模块修订号。只在结构性变化时递增，倒计时递减本身不递增。</summary>
        long Revision { get; }

        bool HasEffect(string actorId, string effectId);
        bool HasEffectWithTag(string actorId, string tag);

        ActiveEffectSnapshot[] GetEffects(string actorId);
        ActiveEffectSnapshot[] GetEffectsByTag(string actorId, string tag);

        bool TryGetEffect(string actorId, string effectInstanceId, out ActiveEffectSnapshot snapshot);
        float GetRemainingSeconds(string actorId, string effectInstanceId);
        int GetStackCount(string actorId, string effectInstanceId);
    }
}
