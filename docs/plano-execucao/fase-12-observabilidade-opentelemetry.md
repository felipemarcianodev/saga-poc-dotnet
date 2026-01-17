# FASE 12: Observabilidade com OpenTelemetry (Jaeger + Serilog + SEQ)


#### 3.12.1 Objetivos
- Implementar distributed tracing com Jaeger via OpenTelemetry
- Coletar logs estruturados com Serilog + SEQ
- Visualizar traces e logs correlacionados
- Rastrear duração e taxa de sucesso das SAGAs end-to-end
- Correlacionar logs e traces via CorrelationId

#### 3.12.2 Entregas

##### 1. **Pacotes NuGet Necessários**

```bash
# Shared (criar projeto SagaPoc.Observability)
dotnet new classlib -n SagaPoc.Observability
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Jaeger
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
dotnet add package Serilog
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Seq
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Thread
dotnet add package Serilog.Enrichers.Process
```

##### 2. **Extension Method para OpenTelemetry**

```csharp
// SagaPoc.Observability/OpenTelemetryExtensions.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
                        options.SetDbStatementForText = true;
                        options.SetDbStatementForStoredProcedure = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.query_type", command.CommandType.ToString());
                        };
                    })
                    // Sources customizados (para criar spans manuais)
                    .AddSource("SagaPoc.*")
                    .AddSource("Rebus") // Para rastrear mensagens RabbitMQ
                    .AddConsoleExporter(); // Console exporter sempre habilitado

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
            });

        return services;
    }

    public static WebApplication UseSagaOpenTelemetry(this WebApplication app)
    {
        return app;
    }
}
```

##### 3. **Configuração nos Serviços**

```csharp
// Program.cs - Orquestrador
using SagaPoc.Observability;

var builder = WebApplication.CreateBuilder(args);

// Adicionar OpenTelemetry
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.Orquestrador",
    serviceVersion: "1.0.0"
);

// ... resto da configuração

var app = builder.Build();

// Habilitar endpoint de métricas
app.UseSagaOpenTelemetry();

app.Run();
```

```csharp
// Program.cs - API
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.Api",
    serviceVersion: "1.0.0"
);
```

```csharp
// Program.cs - Serviços (Restaurante, Pagamento, Entregador, Notificação)
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.ServicoRestaurante", // ou ServiçoPagamento, etc.
    serviceVersion: "1.0.0"
);
```

##### 4. **appsettings.json - Configuração**

```json
{
  "Jaeger": {
    "Enabled": true,
    "AgentHost": "localhost",
    "AgentPort": 6831,
    "Endpoint": "http://localhost:14268/api/traces"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Rebus": "Information"
    }
  }
}
```

##### 5. **Docker Compose - Observability Stack**

```yaml
# docker-compose.yml
version: '3.8'

networks:
  saga-network:
    driver: bridge

services:
  # ===================================
  # OBSERVABILITY - TRACING (Jaeger)
  # ===================================
  jaeger:
    image: jaegertracing/all-in-one:latest
    container_name: saga-jaeger
    environment:
      - COLLECTOR_ZIPKIN_HOST_PORT=:9411
      - COLLECTOR_OTLP_ENABLED=true
    networks:
      - saga-network
    ports:
      - "5775:5775/udp"   # Jaeger Agent (Thrift Compact)
      - "6831:6831/udp"   # Jaeger Agent (Thrift Binary)
      - "6832:6832/udp"   # Jaeger Agent (Thrift HTTP)
      - "5778:5778"       # Jaeger Agent Config
      - "16686:16686"     # Jaeger UI
      - "14250:14250"     # Jaeger Collector gRPC
      - "14268:14268"     # Jaeger Collector HTTP (Thrift)
      - "14269:14269"     # Jaeger Collector Health
      - "9411:9411"       # Zipkin Compatible
    restart: unless-stopped

  # ===================================
  # OBSERVABILITY - STRUCTURED LOGS (SEQ)
  # ===================================
  seq:
    image: datalust/seq:latest
    container_name: saga-seq
    environment:
      - ACCEPT_EULA=Y
    networks:
      - saga-network
    ports:
      - "5341:80"  # SEQ UI e Ingestion
    volumes:
      - seq-data:/data
    restart: unless-stopped

volumes:
  seq-data:
    driver: local
```

##### 6. **Serilog Configuration**

```csharp
// Program.cs - Configuração do Serilog
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .Enrich.WithProperty("Application", "SagaPoc.Orquestrador") // Mudar para cada serviço
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341",
        apiKey: builder.Configuration["Seq:ApiKey"])
    .CreateLogger();

builder.Host.UseSerilog();

// ... resto da configuração

var app = builder.Build();
app.Run();
```

##### 7. **appsettings.json - Configuração SEQ**

```json
{
  "Seq": {
    "ServerUrl": "http://localhost:5341",
    "ApiKey": null
  },
  "Jaeger": {
    "Enabled": true,
    "AgentHost": "localhost",
    "AgentPort": 6831,
    "Endpoint": "http://localhost:14268/api/traces"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

##### 8. **Instrumentação Manual (Opcional)**

```csharp
// Para criar spans customizados
using System.Diagnostics;

public class ServicoRestaurante
{
    private static readonly ActivitySource ActivitySource = new("SagaPoc.ServicoRestaurante");

    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        using var activity = ActivitySource.StartActivity("ValidarPedidoRestaurante");
        activity?.SetTag("restaurante.id", restauranteId);
        activity?.SetTag("itens.count", itens.Count);

        try
        {
            // Lógica de validação...
            var resultado = await ValidarAsync(restauranteId, itens);

            activity?.SetTag("resultado.sucesso", resultado.EhSucesso);

            if (resultado.EhFalha)
            {
                activity?.SetStatus(ActivityStatusCode.Error, resultado.Erro.Mensagem);
            }

            return resultado;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

#### 3.12.3 URLs de Acesso

Após executar `docker-compose up`:

- **Jaeger UI**: http://localhost:16686
- **SEQ UI**: http://localhost:5341

#### 3.12.4 Queries Úteis no SEQ

```sql
-- Filtrar logs por aplicação
Application = "SagaPoc.Orquestrador"

-- Filtrar por CorrelationId
CorrelationId = "a1b2c3d4-e5f6-7890"

-- Eventos de erro
Level = "Error"

-- SAGAs finalizadas com sucesso
@MessageTemplate = "SAGA finalizada com sucesso" AND Estado = "PedidoConfirmado"

-- Eventos de compensação
@MessageTemplate LIKE "%compensação%"
```

#### 3.12.5 Critérios de Aceitação
- [ ] Jaeger exibe traces end-to-end das SAGAs
- [ ] SEQ recebe logs estruturados de todos os serviços
- [ ] Logs incluem CorrelationId para rastreamento
- [ ] Spans incluem tags customizadas (RestauranteId, CorrelacaoId, etc.)
- [ ] Traces mostram propagação através do RabbitMQ
- [ ] Queries no SEQ funcionam com filtros por Application, Level, CorrelationId
- [ ] Correlação entre logs (SEQ) e traces (Jaeger) via CorrelationId

**Estimativa**: 3-4 horas (setup completo + testes)

---

