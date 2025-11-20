using jb.smartchangeover.Service.Domain.Data;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace jb.smartchangeover.Service.Domain.Data
{
    /* This is used if database provider does't define
     * IServiceDbSchemaMigrator implementation.
     */
    public class NullSmartChangeOverDbSchemaMigrator : ISmartChangeOverDbSchemaMigrator, ITransientDependency
    {
        public Task MigrateAsync()
        {
            return Task.CompletedTask;
        }
    }
}