using jb.smartchangeover.Service.Domain;
using jb.smartchangeover.Service.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.MySQL;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.IdentityServer.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;


[DependsOn(
    typeof(SmartChangeOverDomainModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpIdentityServerEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreMySQLModule)
)]
public class SmartChangeOverEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<SmartChangeOverDbContext>(options =>
        {
            /* Add custom repositories here. Example:
             * options.AddRepository<Question, EfCoreQuestionRepository>();
             */
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        Configure<SqlServerConnectionOptions>(options =>
            options.ConnectionStrings = context.Services.GetConfiguration().GetSection($"ConnectionStrings")[SmartChangeOverDbProperties.ConnectionStringName]
        );

        Configure<AbpDbContextOptions>(options =>
        {
            /* The main point to change your DBMS.
             * See also ServiceMigrationsDbContextFactory for EF Core tooling. */
            //options.UseMySQL();

            /*
             * If it is SQL SERVER, use this setting
             * */
            //options.UseSqlServer();

            /*
             * If it is PostgreSql, use this setting
             * */
            options.UsePostgreSql();
        });
    }
}
