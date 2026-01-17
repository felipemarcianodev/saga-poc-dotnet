# FASE 18: Observabilidade com OpenTelemetry


## Objetivos
- Implementar distributed tracing com OpenTelemetry
- Configurar exportação de traces para Jaeger (visualização)
- Configurar logs estruturados com Serilog + SEQ (monitoramento)
- Rastrear duração e status de cada passo da SAGA
- Monitorar taxa de sucesso/falha e compensações

## Contexto

**Problema**: Debug de SAGA distribuída é um pesadelo sem tracing.

**Desafios**:
- Rastrear fluxo através de múltiplos serviços e filas
- Identificar bottlenecks de performance
- Correlacionar erros entre componentes
- Analisar padrões de falha

**Solução**: Adicionar OpenTelemetry + Jaeger (visualização) + Serilog + SEQ (logs)

## Implementação

### 1. Instalar Pacotes NuGet

```bash
dotnet add package OpenTelemetry.Exporter.Jaeger
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package Serilog
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Seq
dotnet add package Serilog.Enrichers.Environment
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
    });
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

### 4. Docker Compose - SEQ

```yaml
seq:
  image: datalust/seq:latest
  container_name: saga-seq
  environment:
    - ACCEPT_EULA=Y
  ports:
    - "5341:80"
  volumes:
    - seq-data:/data
  networks:
    - saga-network

volumes:
  seq-data:
    driver: local
```

**Serilog Configuration** (Program.cs):
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "SagaPoc")
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();
```

**appsettings.json**:
```json
{
  "Seq": {
    "ServerUrl": "http://localhost:5341",
    "ApiKey": null
  }
}
```

**Queries úteis no SEQ**:
```sql
-- Filtrar por aplicação
Application = "SagaPoc.Orquestrador"

-- Filtrar por CorrelationId
CorrelationId = "abc123"

-- SAGAs com falha
Level = "Error" AND @MessageTemplate LIKE "%SAGA%"
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

### Logs Estruturados (SEQ)
- Logs de sucesso/falha por tipo de erro
- Eventos de Dead Letter Queue (DLQ)
- Duração média por estado da SAGA
- Taxa de compensação
- Throughput (pedidos/min)

### Logs Estruturados
- Correlação com TraceId
- Contexto de negócio (RestauranteId, ClienteId, etc)
- Níveis apropriados (Info, Warning, Error)

## Queries no SEQ

### 1. Análise de Taxa de Sucesso

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
- **SEQ UI**: http://localhost:5341

## Critérios de Aceitação
- [ ] OpenTelemetry configurado com Jaeger
- [ ] Distributed tracing funcionando (traces visíveis no Jaeger)
- [ ] Serilog + SEQ configurados e funcionando
- [ ] Logs estruturados com CorrelationId
- [ ] Queries no SEQ retornando dados corretos
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
- [Serilog](https://serilog.net/)
- [SEQ](https://datalust.co/seq)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
