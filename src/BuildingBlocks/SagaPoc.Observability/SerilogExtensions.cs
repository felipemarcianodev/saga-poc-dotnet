using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace SagaPoc.Observability;

/// <summary>
/// Configuração única do Serilog para APIs e workers: console, Seq e, em build Debug, arquivo.
/// Os sinks ficam aqui; o appsettings.json só ajusta os níveis (seção Serilog:MinimumLevel).
/// </summary>
public static class SerilogExtensions
{
    // Sem {Properties}: o texto da mensagem já traz os identificadores (PedidoId, CorrelacaoId),
    // e as propriedades completas vão para o Seq.
    private const string Modelo =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Application}] {SourceContext}: {Message:l}{NewLine}{Exception}";

    #region Public Methods

    /// <summary>
    /// Configura o Serilog de uma API (IHostBuilder).
    /// </summary>
    public static IHostBuilder UseCustomSerilog(this IHostBuilder host)
    {
        host.UseSerilog((context, services, loggerConfiguration) =>
        {
            Configure(loggerConfiguration,
                context.Configuration,
                context.HostingEnvironment.ApplicationName,
                context.HostingEnvironment.EnvironmentName);
        });

        return host;
    }

    /// <summary>
    /// Configura o Serilog de um worker (HostApplicationBuilder). Também define o Log.Logger
    /// estático, usado pelo Rebus (Logging(l => l.Serilog())) e pelos logs de início e fim do Program.
    /// </summary>
    public static HostApplicationBuilder UseCustomSerilog(this HostApplicationBuilder builder,
        IConfiguration configuration,
        string serviceName,
        string environment,
        bool isDevelopment)
    {
        var loggerConfiguration = new LoggerConfiguration();
        Configure(loggerConfiguration, configuration, serviceName, environment);

        Log.Logger = loggerConfiguration.CreateLogger();
        builder.Services.AddSerilog(Log.Logger, dispose: true);

        return builder;
    }

    #endregion Public Methods

    #region Private Methods

    private static void Configure(LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        string serviceName,
        string environment)
    {
        // Níveis padrão; a seção Serilog:MinimumLevel do appsettings, lida em seguida, sobrescreve.
        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Rebus", LogEventLevel.Information)
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithProperty("Application", serviceName)
            .Enrich.WithProperty("Environment", environment)
            .WriteTo.Console(outputTemplate: Modelo);

        // Seq: estruturado, com todas as propriedades
        var seqUrl = configuration["Seq:Url"] ?? configuration["SeqUrl"] ?? "http://localhost:5342";
        var seqApiKey = configuration["Seq:ApiKey"] ?? configuration["SeqApiKey"];

        loggerConfiguration.WriteTo.Seq(
            serverUrl: seqUrl,
            apiKey: string.IsNullOrWhiteSpace(seqApiKey) ? null : seqApiKey
        );

#if DEBUG
        // Arquivo só em build Debug: um por serviço e por dia, em logs/ no diretório de trabalho
        // (no compose, /app/logs montado em logs/ na raiz do repositório). Serve para acompanhar
        // a stack com tail/grep sem depender do docker logs, que some quando o container é recriado.
        loggerConfiguration.WriteTo.File(
            path: Path.Combine("logs", $"{serviceName}-.log"),
            outputTemplate: Modelo,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1)
        );
#endif
    }

    #endregion Private Methods
}
