using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using jb.smartchangeover.Service.Domain.Data;

namespace jb.smartchangeover.Service.EntityFrameworkCore.EntityFrameworkCore
{
    public class EntityFrameworkCoreServiceDbSchemaMigrator
        : ISmartChangeOverDbSchemaMigrator, ITransientDependency
    {
        private readonly IServiceProvider _serviceProvider;

        public EntityFrameworkCoreServiceDbSchemaMigrator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task MigrateAsync()
        {
            /* We intentionally resolving the ServiceMigrationsDbContext
             * from IServiceProvider (instead of directly injecting it)
             * to properly get the connection string of the current tenant in the
             * current scope.
             */

            await _serviceProvider
                .GetRequiredService<SmartChangeOverDbContext>()
                .Database
                .MigrateAsync();
        }
    }
}