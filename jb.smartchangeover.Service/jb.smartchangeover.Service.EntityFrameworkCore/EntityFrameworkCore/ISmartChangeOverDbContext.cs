using jb.smartchangeover.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace jb.smartchangeover.Service.EntityFrameworkCore.EntityFrameworkCore;

[ConnectionStringName(SmartChangeOverDbProperties.ConnectionStringName)]
public interface ISmartChangeOverDbContext : IEfCoreDbContext
{

}
