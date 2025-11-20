using System.IO;
using jb.smartchangeover.Service.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;



public class SmartChangeOverMigrationsDbContextFactory : IDesignTimeDbContextFactory<SmartChangeOverDbContext>
{
    public SmartChangeOverDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<SmartChangeOverDbContext>()
             .UseNpgsql(configuration.GetConnectionString(SmartChangeOverDbProperties.ConnectionStringName));
        //.UseMySql(configuration.GetConnectionString(DemoDbProperties.ConnectionStringName), MySqlServerVersion.LatestSupportedServerVersion);
        //.UseSqlServer(configuration.GetConnectionString(DemoDbProperties.ConnectionStringName))

        return new SmartChangeOverDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}
