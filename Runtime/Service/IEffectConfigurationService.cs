using NiumaAttribute.Service;
using NiumaEffect.Config;

namespace NiumaEffect.Service
{
    /// <summary>
    /// 效果配置能力接口。
    /// 控制器内部使用，避免把配置热更新能力塞进 IEffectService 门面。
    /// </summary>
    public interface IEffectConfigurationService
    {
        void SetDefinitions(EffectDefinition[] definitions);
        void SetTagCatalog(EffectTagCatalog tagCatalog);
        void SetAttributeCommand(IAttributeCommand attributeCommand);
        void SetApplicationResolver(IEffectApplicationResolver resolver);
        void SetImmunityResolver(IEffectImmunityResolver resolver);
    }
}
