# FASE 18: Observabilidade com OpenTelemetry


## Objetivos
- Implementar distributed tracing com OpenTelemetry
- Configurar exportação de traces para Jaeger (visualização)
- Configurar exportação de métricas para Prometheus (monitoramento)
- Rastrear duração e status de cada passo da SAGA
- Monitorar taxa de sucesso/falha e compensações

## Contexto

**Problema**: Debug de SAGA distribuída é um pesadelo sem tracing.

**Desafios**:
- Rastrear fluxo através de múltiplos serviços e filas
- Identificar bottlenecks de performance
- Correlacionar erros entre componentes
- Analisar padrões de falha

**Solução**: Adicionar OpenTelemetry + Jaeger (visualização) + Prometheus (métricas)

## Implementação

### 1. Instalar Pacotes NuGet

```bash
dotnet add package OpenTelemetry.Exporter.Jaeger
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
```

### 2. Configurar OpenTelemetry

```csharp
// Program.cs
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true; // Capturar exceções
                options.EnrichWithHttpRequest = (activity, httpRequest) =>
                {
                    activity.SetTag("http.client_ip", httpRequest.HttpContext.Connection.RemoteIpAddress);
                };
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddSource("Rebus") // Rastrear operações do Rebus
            .AddSource("SagaPOC") // Rastrear operações customizadas
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "localhost";
                options.AgentPort = 6831;
                options.ExportProcessorType = ExportProcessorType.Batch;
                options.MaxPayloadSizeInBytes = 4096;
            })
            // Alternativa: Console Exporter para desenvolvimento
            .AddConsoleExporter();
    })
    .WithMetrics(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Rebus") // Métricas do Rebus
            .AddMeter("SagaPOC") // Métricas customizadas
            .AddPrometheusExporter(); // Para Grafana
    });

// Expor endpoint de métricas Prometheus
app.MapPrometheusScrapingEndpoint(); // /metrics
```

### 3. Docker Compose - Jaeger

```yaml
# Adicionar ao docker-compose.yml
jaeger:
  image: jaegertracing/all-in-one:latest
  container_name: saga-jaeger
  ports:
    - "16686:16686"  # UI
    - "6831:6831/udp"  # Agent (traces)
    - "14268:14268"  # Collector
  environment:
    - COLLECTOR_OTLP_ENABLED=true
  networks:
    - saga-network
```

### 4. Docker Compose - Prometheus + Grafana

```yaml
prometheus:
  image: prom/prometheus:latest
  container_name: saga-prometheus
  ports:
    - "9090:9090"
  volumes:
    - ./prometheus.yml:/etc/prometheus/prometheus.yml
    - prometheus-data:/prometheus
  command:
    - '--config.file=/etc/prometheus/prometheus.yml'
    - '--storage.tsdb.path=/prometheus'
  networks:
    - saga-network

grafana:
  image: grafana/grafana:latest
  container_name: saga-grafana
  ports:
    - "3000:3000"
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=admin
  volumes:
    - grafana-data:/var/lib/grafana
  networks:
    - saga-network
  depends_on:
    - prometheus

volumes:
  prometheus-data:
    driver: local
  grafana-data:
    driver: local
```

**prometheus.yml**:
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'saga-poc'
    static_configs:
      - targets: ['host.docker.internal:5000'] # API
```

### 5. Instrumentar a SAGA

```csharp
using System.Diagnostics;

public class PedidoSaga : Saga<PedidoSagaData>
{
    private static readonly ActivitySource ActivitySource = new("SagaPOC");
    private readonly ILogger<PedidoSaga> _logger;

    public PedidoSaga(ILogger<PedidoSaga> logger)
    {
        _logger = logger;

        Initially(
            When(IniciarPedido)
                .Then(async context =>
                {
                    using var activity = ActivitySource.StartActivity("SAGA.IniciarPedido");
                    activity?.SetTag("saga.correlation_id", context.Saga.CorrelationId);
                    activity?.SetTag("saga.restaurante_id", context.Message.RestauranteId);
                    activity?.SetTag("saga.cliente_id", context.Message.ClienteId);

                    context.Saga.RestauranteId = context.Message.RestauranteId;
                    context.Saga.ClienteId = context.Message.ClienteId;
                    context.Saga.Itens = context.Message.Itens;

                    _logger.LogInformation(
                        "[SAGA] Iniciando pedido {CorrelacaoId} - TraceId: {TraceId}",
                        context.Saga.CorrelationId,
                        activity?.TraceId
                    );
                })
                .TransitionTo(ValidandoRestaurante)
                .Publish(context => new ValidarPedidoRestaurante(
                    context.Saga.CorrelationId,
                    context.Message.RestauranteId,
                    context.Message.Itens
                ))
        );

        During(ValidandoRestaurante,
            When(PedidoRestauranteValidado)
                .Then(async context =>
                {
                    using var activity = ActivitySource.StartActivity("SAGA.RestauranteValidado");
                    activity?.SetTag("saga.correlation_id", context.Saga.CorrelationId);
                    activity?.SetTag("saga.valido", context.Message.Valido);
                    activity?.SetTag("saga.valor_total", context.Message.ValorTotal);

                    if (context.Message.Valido)
                    {
                        context.Saga.ValorTotal = context.Message.ValorTotal;
                        activity?.AddEvent(new ActivityEvent("RestauranteValidadoComSucesso"));
                    }
                    else
                    {
                        activity?.AddEvent(new ActivityEvent("RestauranteRejeitado"));
                        activity?.SetStatus(ActivityStatusCode.Error, context.Message.MotivoRejeicao);
                    }
                })
                .IfElse(
                    context => context.Message.Valido,
                    validado => validado
                        .TransitionTo(ProcessandoPagamento)
                        .Publish(context => new ProcessarPagamento(/* ... */)),
                    rejeitado => rejeitado
                        .TransitionTo(Compensando)
                        .Publish(context => new CompensarPedido(/* ... */))
                )
        );

        // Repetir para todos os estados...
    }
}
```

### 6. Instrumentar Consumers

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private static readonly ActivitySource ActivitySource = new("SagaPOC");
    private readonly IServicoPagamento _servico;
    private readonly ILogger<ProcessarPagamentoConsumer> _logger;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        using var activity = ActivitySource.StartActivity("Consumer.ProcessarPagamento");
        activity?.SetTag("correlation_id", context.Message.CorrelacaoId);
        activity?.SetTag("valor", context.Message.ValorTotal);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var resultado = await _servico.ProcessarAsync(
                context.Message.ClienteId,
                context.Message.ValorTotal,
                context.Message.FormaPagamento,
                context.CancellationToken
            );

            stopwatch.Stop();

            activity?.SetTag("resultado", resultado.EhSucesso ? "sucesso" : "falha");
            activity?.SetTag("duracao_ms", stopwatch.ElapsedMilliseconds);

            if (resultado.EhSucesso)
            {
                activity?.AddEvent(new ActivityEvent("PagamentoProcessadoComSucesso"));
                activity?.SetTag("transacao_id", resultado.Valor.TransacaoId);
            }
            else
            {
                activity?.AddEvent(new ActivityEvent("PagamentoFalhou"));
                activity?.SetStatus(ActivityStatusCode.Error, resultado.Erro.Mensagem);
            }

            await context.RespondAsync(new PagamentoProcessado(
                context.Message.CorrelacaoId,
                resultado.EhSucesso,
                resultado.EhSucesso ? resultado.Valor.TransacaoId : null,
                resultado.EhFalha ? resultado.Erro.Mensagem : null
            ));
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

### 7. Métricas Customizadas

```csharp
using System.Diagnostics.Metrics;

public class SagaMetrics
{
    private static readonly Meter Meter = new("SagaPOC");

    public static readonly Counter<long> PedidosIniciados = Meter.CreateCounter<long>(
        "saga.pedidos.iniciados",
        description: "Total de pedidos iniciados"
    );

    public static readonly Counter<long> PedidosCompletos = Meter.CreateCounter<long>(
        "saga.pedidos.completos",
        description: "Total de pedidos completados com sucesso"
    );

    public static readonly Counter<long> PedidosFalhados = Meter.CreateCounter<long>(
        "saga.pedidos.falhados",
        description: "Total de pedidos que falharam"
    );

    public static readonly Counter<long> Compensacoes = Meter.CreateCounter<long>(
        "saga.compensacoes.executadas",
        description: "Total de compensações executadas"
    );

    public static readonly Histogram<double> DuracaoSaga = Meter.CreateHistogram<double>(
        "saga.duracao.segundos",
        unit: "s",
        description: "Duração total da SAGA em segundos"
    );
}

// Usar nas transições da SAGA
Initially(
    When(IniciarPedido)
        .Then(context =>
        {
            SagaMetrics.PedidosIniciados.Add(1, new KeyValuePair<string, object>("restaurante_id", context.Message.RestauranteId));
            context.Saga.DataInicio = DateTime.UtcNow;
        })
);

SetCompleted(
    When(PedidoEntregue)
        .Then(context =>
        {
            var duracao = (DateTime.UtcNow - context.Saga.DataInicio).TotalSeconds;
            SagaMetrics.DuracaoSaga.Record(duracao);
            SagaMetrics.PedidosCompletos.Add(1);
        })
);
```

## O que Rastrear

### Traces (Distributed Tracing)
- Duração de cada passo da SAGA
- Correlação entre mensagens (CorrelationId)
- Exceções e erros
- Compensações executadas
- Latência de comunicação com RabbitMQ

### Métricas (Prometheus)
- Taxa de sucesso/falha por tipo de erro
- Dead Letter Queue (DLQ) metrics
- Duração média por estado da SAGA
- Taxa de compensação
- Throughput (pedidos/min)

### Logs Estruturados
- Correlação com TraceId
- Contexto de negócio (RestauranteId, ClienteId, etc)
- Níveis apropriados (Info, Warning, Error)

## Dashboards Grafana

### 1. Dashboard de Taxa de Sucesso

```promql
# Taxa de sucesso (últimas 5 min)
rate(saga_pedidos_completos_total[5m]) / rate(saga_pedidos_iniciados_total[5m])

# Taxa de falha
rate(saga_pedidos_falhados_total[5m]) / rate(saga_pedidos_iniciados_total[5m])

# Taxa de compensação
rate(saga_compensacoes_executadas_total[5m])
```

### 2. Dashboard de Performance

```promql
# P95 de duração da SAGA
histogram_quantile(0.95, saga_duracao_segundos_bucket)

# P99 de duração da SAGA
histogram_quantile(0.99, saga_duracao_segundos_bucket)

# Duração média
rate(saga_duracao_segundos_sum[5m]) / rate(saga_duracao_segundos_count[5m])
```

## Acessar Ferramentas

- **Jaeger UI**: http://localhost:16686
- **Prometheus UI**: http://localhost:9090
- **Grafana**: http://localhost:3000 (admin/admin)

## Critérios de Aceitação
- [ ] OpenTelemetry configurado com Jaeger e Prometheus
- [ ] Distributed tracing funcionando (traces visíveis no Jaeger)
- [ ] Métricas customizadas implementadas
- [ ] Dashboard Grafana criado com métricas principais
- [ ] Logs estruturados com TraceId
- [ ] Documentação de observabilidade atualizada
- [ ] Runbook de troubleshooting com Jaeger

## Estimativa
**4-6 horas** (setup + instrumentação + dashboards básicos)

## Próximos Passos
- [Fase 19 - Retry Policy e Circuit Breaker](./fase-19.md)
- [Fase 20 - Idempotência](./fase-20.md)

## Referências
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Jaeger](https://www.jaegertracing.io/)
- [Prometheus](https://prometheus.io/)
- [Grafana](https://grafana.com/)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
