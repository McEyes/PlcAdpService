using jb.smartchangeover.Service.Application.Contracts;
using jb.smartchangeover.Service.EntityFrameworkCore.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace jb.smartchangeover.Service.DbMigrator
{
    [DependsOn(
        typeof(AbpAutofacModule),
        typeof(SmartChangeOverEntityFrameworkCoreModule),
        typeof(SmartChangeOverApplicationContractsModule)
        )]
    public class ServiceDbMigratorModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddTransient<Domain.Data.ISmartChangeOverDbSchemaMigrator, EntityFrameworkCoreServiceDbSchemaMigrator>();

            Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
        }
    }
}