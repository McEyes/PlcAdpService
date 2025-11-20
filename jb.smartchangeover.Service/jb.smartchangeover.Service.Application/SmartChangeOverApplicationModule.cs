using AoiAdapterService.Mqtts;
using Jabil.Service.Frameworks;
using jb.smartchangeover.Service.Application.Handlers;
using jb.smartchangeover.Service.Application.Mqtts;
using jb.smartchangeover.Service.Domain;
using jb.smartchangeover.Service.Domain.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Application;
using Volo.Abp.AutoMapper;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;

namespace jb.smartchangeover.Service.Application
{
    [DependsOn(
        typeof(SmartChangeOverDomainModule),
        typeof(AbpDddApplicationModule),
        typeof(AbpAutoMapperModule),
        typeof(AbpIdentityApplicationModule),
        typeof(AbpPermissionManagementApplicationModule),
        typeof(JabilServiceFrameworksModule)
        )]
    public class SmartChangeOverApplicationModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options =>
            {
                options.AddMaps<SmartChangeOverApplicationModule>();
            });
            context.Services
                .AddSingleton<IMqttClientService, MqttService>();
        }

        public override async void OnPostApplicationInitialization(ApplicationInitializationContext context)
        {
            //context.KafkaConsumerStartup();

            base.OnPostApplicationInitialization(context);
            //var a = context.ServiceProvider.GetService<DekExecuteHandler>();
            //await a.StartAsync();
            var a = context.ServiceProvider.GetService<PlcHandler>();
            await a.StartAsync();
        }

    }
}
