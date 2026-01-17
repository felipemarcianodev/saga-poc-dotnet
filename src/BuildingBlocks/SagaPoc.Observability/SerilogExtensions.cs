using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SagaPoc.Observability;

/// <summary>
/// Extension methods para configurar Serilog com SEQ para a SAGA POC.
/// </summary>
public static class SerilogExtensions
{
    #region Public Methods

    /// <summary>
    /// Configura Serilog com enrichers e sinks padrão para WebApplicationBuilder/IHostBuilder.
    /// </summary>
    /// <param name="host">Host builder</param>
    /// <param name="serviceName">Nome do serviço (ex: "SagaPoc.Api", "SagaPoc.Orquestrador")</param>
    /// <returns>Host builder configurado</returns>
    public static IHostBuilder UseCustomSerilog(this IHostBuilder host)
    {
        host.UseSerilog((context, services, loggerConfiguration) =>
        {
            Configure(loggerConfiguration,
                context.Configuration,
                context.HostingEnvironment.ApplicationName,
                context.HostingEnvironment.EnvironmentName,
                context.HostingEnvironment.IsDevelopment());
        });

        return host;
    }

    /// <summary>
    /// Configura Serilog com enrichers e sinks padrão para HostApplicationBuilder (Worker Services).
    /// </summary>
    /// <param name="builder">Host application builder</param>
    /// <param name="serviceName">Nome do serviço (ex: "SagaPoc.Orquestrador")</param>
    /// <returns>Host application builder configurado</returns>
    public static HostApplicationBuilder UseCustomSerilog(this HostApplicationBuilder builder,
        IConfiguration configuration,
        string serviceName,
        string environment,
        bool isDeveloment)
    {
        var loggerConfiguration = new LoggerConfiguration();
        Configure(loggerConfiguration, configuration, serviceName, environment, isDeveloment);

        Log.Logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        //.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                        .Build())
                    .Enrich.FromLogContext()
                    .CreateLogger();
        builder.Services.AddSerilog(Log.Logger);

        return builder;
    }

    #endregion Public Methods

    #region Private Methods

    private static void Configure(LoggerConfiguration loggerConfiguration,
            IConfiguration configuration,
        string serviceName,
        string environment,
        bool isDeveloment)
    {
        // Configuração base
        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Rebus", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Application", serviceName)
            .Enrich.WithProperty("Environment", environment);

        // Sink: Arquivo (JSON estruturado para persistência local)
        loggerConfiguration.WriteTo.File(
            formatter: new Serilog.Formatting.Json.JsonFormatter(),
            path: "logs/saga-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true
        );

        // Sink: SEQ (estruturado com detalhes completos)
        var seqUrl = configuration["Seq:Url"] ?? configuration["SeqUrl"] ?? "http://localhost:5342";
        var seqApiKey = configuration["Seq:ApiKey"] ?? configuration["SeqApiKey"];

        loggerConfiguration.WriteTo.Seq(
            serverUrl: seqUrl,
            restrictedToMinimumLevel: LogEventLevel.Verbose,
            apiKey: seqApiKey
        );

        // Configuração específica por ambiente
        if (isDeveloment)
        {
            // Development: Console colorido e legível
            loggerConfiguration
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
                .MinimumLevel.Override("Rebus", LogEventLevel.Information)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{Application}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
        else
        {
            // Production: Console estruturado (JSON)
            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Rebus", LogEventLevel.Warning)
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Application}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }
    }

    #endregion Private Methods
}