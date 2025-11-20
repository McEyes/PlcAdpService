using System.Threading.Tasks;

namespace jb.smartchangeover.Service.Domain.Data
{
    public interface ISmartChangeOverDbSchemaMigrator
    {
        Task MigrateAsync();
    }
}
