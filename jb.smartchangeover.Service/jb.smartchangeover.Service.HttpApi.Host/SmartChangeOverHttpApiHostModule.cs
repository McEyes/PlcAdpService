using Microsoft.AspNetCore.Cors;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Modularity;
using Jabil.Service.Shared.Hosting.Microservices.Microsoft.AspNetCore.Builder;
using Jabil.Service.Shared.Hosting.Microservices.Swaggers;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Jabil.Service.Shared.Hosting.Microservices.Microsoft.AspNetCore.Filters;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using Volo.Abp.Caching;
using Volo.Abp.Localization;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.EntityFrameworkCore;
using Jabil.Service.Shared.Hosting.Microservices;
using jb.smartchangeover.Service.Application;
using Jabil.Service.Frameworks.Minio;
using Volo.Abp.Auditing;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Volo.Abp.AspNetCore.Mvc;

namespace jb.smartchangeover.Service.HttpApi.Host;

[DependsOn(
        typeof(SharedHostingMicroserviceModule),
        typeof(SmartChangeOverApplicationModule),
        typeof(SmartChangeOverHttpApiModule),
        typeof(SmartChangeOverEntityFrameworkCoreModule),
        typeof(AbpAutofacModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpAspNetCoreSerilogModule)
)]
public class SmartChangeOverHttpApiHostModule : AbpModule
{
    private const string DefaultCorsPolicyName = "Default";
    private const string ApplicationName = "PlcAdapterServer";

    public override void OnPostApplicationInitialization(ApplicationInitializationContext context)
    {
        base.OnPostApplicationInitialization(context);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        ConfigureSwaggerServices(context);
        ConfigureJwtAuthentication(context, configuration);
        ConfigAntiForgery();
        ConfigureCache(context);
        ConfigureCors(context);
        ConfigureDB();
        ConfigureAbpExceptions(context);
        ConfigureLocalization();
        ConfigureVirtualFileSystem(context);
        ConfigureOptions(context);
        ConfigureAuditingOptions(context);
        ConfigureResponseJsonFormat(context);
        ConfigureConventionalControllers();
        context.Services.AddHttpClient();
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var configuration = context.GetConfiguration();

        app.UseHttpsRedirection();
        app.UseCorrelationId();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseCors(DefaultCorsPolicyName);
        app.UseAuthentication();
        app.UseAbpRequestLocalization();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{ApplicationName} Service API");
            options.DocExpansion(DocExpansion.None);
            options.DefaultModelExpandDepth(-2);
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints(endpoints => { endpoints.MapHealthChecks("/health"); });

        if (configuration.GetValue<bool>("Consul:Enabled"))
        {
            app.UseConsul();
        }
    }

    private void ConfigureCache(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = $"{ApplicationName}:"; });

        if (!hostingEnvironment.IsDevelopment())
        {
            var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
            context.Services
                .AddDataProtection()
                .PersistKeysToStackExchangeRedis(redis, $"{ApplicationName}-Protection-Keys");
        }
    }

    private void ConfigureOptions(ServiceConfigurationContext context)
    {
        context.Services.Configure<MinioOptions>(context.Services.GetConfiguration()
                .GetSection("Minio"));
    }

    private void ConfigureDB()
    {
        Configure<AbpDbContextOptions>(options => { options.UsePostgreSql(); });
        //Configure<AbpDbContextOptions>(options => { options.UseMySQL(); });

        /*
         * If it is SQL SERVER, use this setting
         * */
        //Configure<AbpDbContextOptions>(options => { options.UseSqlServer(); });
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
        });
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SmartChangeOverHttpApiModule>();
        });
    }

    private static void ConfigureSwaggerServices(ServiceConfigurationContext context)
    {
        context.Services.AddSwaggerGen(
            options =>
            {
                // Set download file type
                options.MapType<FileContentResult>(() => new OpenApiSchema() { Type = "file" });
                options.SwaggerDoc("v1", new OpenApiInfo { Title = $"{ApplicationName} Service API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.EnableAnnotations();
                options.DocumentFilter<HiddenAbpDefaultApiFilter>();
                options.SchemaFilter<EnumSchemaFilter>();
                var xmlPaths = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
                foreach (var xml in xmlPaths)
                {
                    options.IncludeXmlComments(xml, true);
                }

                options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme,
                new OpenApiSecurityScheme()
                {
                    Description = "Enter the Token generated by JWT directly in the box below",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = "JWT"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme, Id = "Bearer"
                            }
                        },
                        new List<string>()
                    }
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                                { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                        },
                        Array.Empty<string>()
                    }
                });
            });
    }
    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(SmartChangeOverApplicationModule).Assembly);
        });
    }

    private void ConfigAntiForgery()
    {
        Configure<AbpAntiForgeryOptions>(options => { options.AutoValidate = false; });
    }

    private void ConfigureAuditingOptions(ServiceConfigurationContext context)
    {
        Configure<AbpAuditingOptions>(options => { options.ApplicationName = ApplicationName; });
    }

    private void ConfigureAbpExceptions(ServiceConfigurationContext context)
    {
        context.Services.AddMvc(options =>
        {
            options.Filters.Add(typeof(ResultExceptionFilter));
        });
    }

    private void ConfigureResponseJsonFormat(ServiceConfigurationContext context)
    {
        context.Services.AddMvc().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
        });
    }

    private void ConfigureCors(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddCors(options =>
        {
            options.AddPolicy(DefaultCorsPolicyName, builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.RemovePostFix("/"))
                            .ToArray()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    private void ConfigureJwtAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters =
            new TokenValidationParameters()
            {
                ValidateIssuerSigningKey = true,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(configuration["Jwt:SecurityKey"] ?? ""))
            };
        });
    }
}