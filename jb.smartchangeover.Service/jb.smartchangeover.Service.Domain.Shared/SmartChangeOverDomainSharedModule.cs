using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace jb.smartchangeover.Service.Domain.Shared
{
    [DependsOn()]
    public class SmartChangeOverDomainSharedModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            SmartChangeOverDomainGlobalFeatureConfigurator.Configure();
            SmartChangeOverModuleExtensionConfigurator.Configure();
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.AddEmbedded<SmartChangeOverDomainSharedModule>(SmartChangeOverDomainSharedConsts.NameSpace);
            });
        }
    }
}