using jb.smartchangeover.Service.Domain;
using jb.smartchangeover.Service.EntityFrameworkCore.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

[ConnectionStringName(SmartChangeOverDbProperties.ConnectionStringName)]
public class SmartChangeOverDbContext : AbpDbContext<SmartChangeOverDbContext>, ISmartChangeOverDbContext
{
    
    public SmartChangeOverDbContext(DbContextOptions<SmartChangeOverDbContext> options)
        : base(options)
    {
      
        //Console.WriteLine("options=>" + JsonConvert.SerializeObject(options));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureDemo();
    }
}
