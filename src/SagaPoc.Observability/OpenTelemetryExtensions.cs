using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace SagaPoc.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddSagaOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment,
                ["service.namespace"] = "SagaPoc",
                ["host.name"] = Environment.MachineName
            });

        // Verificar se Jaeger está habilitado
        var jaegerEnabled = configuration.GetValue("Jaeger:Enabled", true);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddTelemetrySdk())
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    // Instrumentação automática do ASP.NET Core
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                            activity.SetTag("http.user_agent", request.Headers["User-Agent"].ToString());
                            activity.SetTag("http.request_content_length", request.ContentLength);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response_content_length", response.ContentLength);
                        };
                    })
                    // Instrumentação de HttpClient
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                        };
                    })
                    // Instrumentação do Entity Framework Core (se usar PostgreSQL para SAGA state)
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.query_type", command.CommandType.ToString());
                        };
                    })
                    // Sources customizados (para criar spans manuais)
                    .AddSource("SagaPoc.*")
                    .AddSource("MassTransit"); // Para rastrear mensagens RabbitMQ

                // Adicionar Jaeger Exporter (HTTP Binary Thrift)
                if (jaegerEnabled)
                {
                    tracing.AddJaegerExporter(options =>
                    {
                        var jaegerEndpoint = configuration["Jaeger:Endpoint"] ?? "http://localhost:14268/api/traces";
                        options.AgentHost = configuration["Jaeger:AgentHost"] ?? "localhost";
                        options.AgentPort = int.Parse(configuration["Jaeger:AgentPort"] ?? "6831");
                        options.MaxPayloadSizeInBytes = 4096;
                        options.ExportProcessorType = ExportProcessorType.Batch;
                        options.Endpoint = new Uri(jaegerEndpoint);
                        options.Protocol = JaegerExportProtocol.HttpBinaryThrift;
                        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                        {
                            MaxQueueSize = 2048,
                            ScheduledDelayMilliseconds = 5000,
                            ExporterTimeoutMilliseconds = 30000,
                            MaxExportBatchSize = 512,
                        };
                    });
                }
            })
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("SagaPoc.*")
                .AddMeter("MassTransit")
                .AddPrometheusExporter());

        return services;
    }

    public static WebApplication UseSagaOpenTelemetry(this WebApplication app)
    {
        // Endpoint de métricas Prometheus (/metrics)
        app.MapPrometheusScrapingEndpoint();

        return app;
    }

    /// <summary>
    /// Adiciona OpenTelemetry para serviços Host (não WebApplication).
    /// Para expor métricas Prometheus, o serviço Host precisa adicionar AspNetCore manualmente.
    /// </summary>
    public static IHostApplicationBuilder AddSagaOpenTelemetryForHost(
        this IHostApplicationBuilder builder,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        return builder.AddSagaOpenTelemetryForHost(builder.Configuration, serviceName, serviceVersion);
    }

    /// <summary>
    /// Adiciona OpenTelemetry para serviços Host com configuração customizada.
    /// </summary>
    public static IHostApplicationBuilder AddSagaOpenTelemetryForHost(
        this IHostApplicationBuilder builder,
        IConfiguration configuration,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        builder.Services.AddSagaOpenTelemetry(configuration, serviceName, serviceVersion);
        return builder;
    }
}
