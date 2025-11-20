using Jabil.Service.Frameworks;
using Jabil.Service.Frameworks.Etcd.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using System;

namespace jb.smartchangeover.Service.HttpApi.Host;

public class Program
{
    public static void Main(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);

        CreateHostBuilder(args).Build().Run();
    }
    internal static IHostBuilder CreateHostBuilder(string[] args) =>
    Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseEtcdConfig(reloadOnChange: true);
        webBuilder.UseStartup<Startup>();
    })
    .UseSerilog((context, loggerConfiguration) =>
    {
        SerilogToRabbitMq.SetConfiguration(loggerConfiguration, context.Configuration);
    })
    .UseAutofac();
}
