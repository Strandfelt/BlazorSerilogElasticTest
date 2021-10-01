using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using System.IO;

namespace BlazorSerilogElasticTest.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();

            try
            {
                Log.Information("Starting web host");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    var keyVaultURL = config.Build().GetValue<string>("Vault:KeyVaultURL");
                    if (!string.IsNullOrWhiteSpace(keyVaultURL))
                    {
                        config.AddAzureKeyVault(keyVaultURL);
                    }
                    config.AddEnvironmentVariables();
                })
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console()
                    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(context.Configuration[context.Configuration["Vault:ElasticURISecretName"]]))
                    {
                        IndexFormat = "logging-blazor-test-{0:yyyy.MM.dd}",
                        MinimumLogEventLevel = LogEventLevel.Information,
                        EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog,
                        DeadLetterIndexName = "blazor-test-deadletter-{0:yyyy.MM.dd}",
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                        RegisterTemplateFailure = RegisterTemplateRecovery.IndexAnyway
                    }))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
