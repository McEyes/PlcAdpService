using jb.smartchangeover.Service.Domain.Shared;
using Volo.Abp.Account;
using Volo.Abp.Application;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.PermissionManagement;

namespace jb.smartchangeover.Service.Application.Contracts
{
    [DependsOn(
        typeof(SmartChangeOverDomainSharedModule),
        typeof(AbpObjectExtendingModule),
        typeof(AbpAccountApplicationContractsModule),
        typeof(AbpIdentityApplicationContractsModule),
        typeof(AbpPermissionManagementApplicationContractsModule),
        typeof(AbpDddApplicationContractsModule),
        typeof(AbpAuthorizationModule)
    )]
    public class SmartChangeOverApplicationContractsModule : AbpModule
    {
        
    }
}
