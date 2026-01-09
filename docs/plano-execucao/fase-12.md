# FASE 12: Observabilidade com OpenTelemetry (Jaeger + Prometheus + Grafana)


#### 3.12.1 Objetivos
- Implementar distributed tracing com Jaeger via OpenTelemetry
- Coletar métricas com Prometheus
- Visualizar métricas e traces no Grafana
- Rastrear duração e taxa de sucesso das SAGAs end-to-end
- Correlacionar logs, métricas e traces

#### 3.12.2 Entregas

##### 1. **Pacotes NuGet Necessários**

```bash
# Shared (criar projeto SagaPoc.Observability)
dotnet new classlib -n SagaPoc.Observability
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Jaeger
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
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
            })
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter("SagaPoc.*")
                .AddMeter("Rebus")
                .AddPrometheusExporter());

        return services;
    }

    public static WebApplication UseSagaOpenTelemetry(this WebApplication app)
    {
        // Endpoint de métricas Prometheus (/metrics)
        app.MapPrometheusScrapingEndpoint();

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
  # OBSERVABILITY - METRICS (Prometheus)
  # ===================================
  prometheus:
    image: prom/prometheus:latest
    container_name: saga-prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'
    volumes:
      - ./infra/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    networks:
      - saga-network
    ports:
      - "9090:9090"
    restart: unless-stopped

  # ===================================
  # OBSERVABILITY - DASHBOARDS (Grafana)
  # ===================================
  grafana:
    image: grafana/grafana:latest
    container_name: saga-grafana
    environment:
      - GF_SECURITY_ADMIN_USER=admin
      - GF_SECURITY_ADMIN_PASSWORD=admin123
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana-data:/var/lib/grafana
      - ./infra/grafana/datasources:/etc/grafana/provisioning/datasources:ro
      - ./infra/grafana/dashboards.yml:/etc/grafana/provisioning/dashboards/dashboards.yml:ro
    networks:
      - saga-network
    ports:
      - "3000:3000"
    depends_on:
      - prometheus
      - jaeger
    restart: unless-stopped

  # ===================================
  # OBSERVABILITY - NODE EXPORTER (Opcional)
  # ===================================
  node-exporter:
    image: prom/node-exporter:latest
    container_name: saga-node-exporter
    command:
      - '--path.procfs=/host/proc'
      - '--path.sysfs=/host/sys'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/rootfs:ro
    networks:
      - saga-network
    ports:
      - "9100:9100"
    restart: unless-stopped

volumes:
  prometheus-data:
    driver: local
  grafana-data:
    driver: local
```

##### 6. **Prometheus Configuration**

```yaml
# infra/prometheus/prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  # Prometheus self-monitoring
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']

  # Node Exporter (system metrics)
  - job_name: 'node-exporter'
    static_configs:
      - targets: ['node-exporter:9100']

  # .NET APIs
  - job_name: 'saga-api'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5000']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-api'

  - job_name: 'saga-orquestrador'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5001']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-orquestrador'

  - job_name: 'saga-servico-restaurante'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5002']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-restaurante'

  - job_name: 'saga-servico-pagamento'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5003']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-pagamento'

  - job_name: 'saga-servico-entregador'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5004']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-entregador'

  - job_name: 'saga-servico-notificacao'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5005']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-notificacao'

  # RabbitMQ (se habilitar prometheus plugin)
  - job_name: 'rabbitmq'
    static_configs:
      - targets: ['rabbitmq:15692']
```

##### 7. **Grafana Datasources Configuration**

```yaml
# infra/grafana/datasources/datasources.yml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: false

  - name: Jaeger
    type: jaeger
    access: proxy
    url: http://jaeger:16686
    editable: false
```

```yaml
# infra/grafana/dashboards.yml
apiVersion: 1

providers:
  - name: 'Saga POC Dashboards'
    orgId: 1
    folder: ''
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /var/lib/grafana/dashboards
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
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (admin/admin123)

#### 3.12.4 Queries Úteis no Prometheus

```promql
# Taxa de requisições por segundo
rate(http_server_requests_total[5m])

# Duração P95 das requisições
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# Taxa de erro
rate(http_server_requests_total{status=~"5.."}[5m])
```

#### 3.12.5 Critérios de Aceitação
- [ ] Jaeger exibe traces end-to-end das SAGAs
- [ ] Prometheus coleta métricas de todos os serviços
- [ ] Grafana conectado ao Prometheus e Jaeger
- [ ] Spans incluem tags customizadas (RestauranteId, CorrelacaoId, etc.)
- [ ] Traces mostram propagação através do RabbitMQ
- [ ] Métricas de duração e taxa de erro funcionando

**Estimativa**: 4-6 horas (setup completo + testes)

---

