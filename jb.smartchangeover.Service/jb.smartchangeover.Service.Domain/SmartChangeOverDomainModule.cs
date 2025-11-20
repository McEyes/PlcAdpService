using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using jb.smartchangeover.Service.Domain.Shared;
using Volo.Abp.AuditLogging;
using Volo.Abp.AutoMapper;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.IdentityServer;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Caching;

namespace jb.smartchangeover.Service.Domain
{
    [DependsOn(
        typeof(AbpCachingModule),
        typeof(SmartChangeOverDomainSharedModule),
        typeof(AbpIdentityDomainModule),
        typeof(AbpPermissionManagementDomainIdentityModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpIdentityServerDomainModule)
    )]
    public class SmartChangeOverDomainModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                options.AddMaps<SmartChangeOverDomainModule>();
            });
        }
    }
}
