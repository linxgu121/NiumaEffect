using NiumaEffect.Data;
using NiumaEffect.Result;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 效果服务门面接口。
    /// 组合查询、命令和快照导入导出能力。
    /// </summary>
    public interface IEffectService : IEffectQuery, IEffectCommand
    {
        EffectSaveData ExportSnapshot();
        EffectOperationResult ImportSnapshot(EffectSaveData saveData);
    }
}
