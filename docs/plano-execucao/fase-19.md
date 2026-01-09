# FASE 19: Retry Policy e Circuit Breaker


## Objetivos
- Implementar retry policy com exponential backoff para falhas transitórias
- Configurar Circuit Breaker para proteger serviços downstream
- Diferenciar erros transitórios de erros permanentes
- Implementar Dead Letter Queue (DLQ) para mensagens com falha persistente

## Contexto

**Problema**: Falhas transitórias (timeout, network blip, serviço momentaneamente indisponível) matam a SAGA.

**Cenários de Falha Transitória**:
- Timeout de rede temporário
- Serviço externo momentaneamente indisponível (503)
- Deadlock temporário no banco de dados
- Rate limiting temporário

**Cenários de Falha Permanente** (não fazer retry):
- Validação de negócio (produto indisponível)
- Dados inválidos (valor negativo)
- Recurso não encontrado (404)
- Autenticação/Autorização (401/403)

## Implementação

### 1. Configurar Retry Policy no Rebus

```csharp
// Program.cs
services.AddRebus(configure =>
{
    configure
        .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, "saga-queue"))
        .Options(o =>
        {
            // Retry Policy: Exponential Backoff
            o.SimpleRetryStrategy(
                maxDeliveryAttempts: 5,
                secondLevelRetriesEnabled: true
            );

            // Configurar intervalo entre retries
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(1);
        })
        .Routing(r => r.TypeBased().MapAssemblyOf<IniciarPedido>("saga-queue"))
        .Logging(l => l.Serilog());
});

// Configuração de Second Level Retries (retry com delay)
services.AddRebus(configure =>
{
    configure
        .Options(o =>
        {
            o.SimpleRetryStrategy(
                maxDeliveryAttempts: 3, // Retry imediato (primeira linha de defesa)
                secondLevelRetriesEnabled: true
            );
        })
        .Timeouts(t => t.StoreInMemory()) // Para second-level retries
        .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, "saga-queue"))
        .Options(o =>
        {
            o.RetryStrategy(
                firstLevelRetries: 3, // Retry imediato
                secondLevelRetries: 5, // Retry com exponential backoff
                errorDetailsHeaderMaxLength: 10000
            );
        });
});
```

### 2. Exponential Backoff Customizado

```csharp
public class ExponentialBackoffRetryStrategy : IRetryStrategy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _initialDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeSpan _deltaBackoff;

    public ExponentialBackoffRetryStrategy(
        int maxAttempts = 5,
        TimeSpan? initialDelay = null,
        TimeSpan? maxDelay = null,
        TimeSpan? deltaBackoff = null)
    {
        _maxAttempts = maxAttempts;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);
        _maxDelay = maxDelay ?? TimeSpan.FromSeconds(30);
        _deltaBackoff = deltaBackoff ?? TimeSpan.FromSeconds(5);
    }

    public IEnumerable<TimeSpan> GetRetryIntervals()
    {
        for (int i = 0; i < _maxAttempts; i++)
        {
            var delay = TimeSpan.FromMilliseconds(
                Math.Min(
                    _initialDelay.TotalMilliseconds * Math.Pow(2, i) + _deltaBackoff.TotalMilliseconds,
                    _maxDelay.TotalMilliseconds
                )
            );

            yield return delay;
        }
    }
}

// Usar na configuração
services.AddRebus(configure =>
{
    configure.Options(o =>
    {
        o.RetryStrategy(new ExponentialBackoffRetryStrategy(
            maxAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(30),
            deltaBackoff: TimeSpan.FromSeconds(5)
        ));
    });
});
```

**Exemplo de intervalos gerados**:
```
Tentativa 1: Imediato
Tentativa 2: 1s
Tentativa 3: 7s (2^1 * 1s + 5s)
Tentativa 4: 14s (2^2 * 1s + 5s)
Tentativa 5: 30s (2^3 * 1s + 5s, limitado a maxDelay)
```

### 3. Configurar Circuit Breaker (usando Polly)

```bash
dotnet add package Polly
dotnet add package Polly.Extensions.Http
```

```csharp
// Program.cs
services.AddHttpClient<IServicoPagamento, ServicoPagamento>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // 5xx, 408, network failures
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Console.WriteLine($"[Retry] Tentativa {retryAttempt} após {timespan.TotalSeconds}s");
            }
        );
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5, // Abrir circuito após 5 falhas consecutivas
            durationOfBreak: TimeSpan.FromSeconds(30), // Manter circuito aberto por 30s
            onBreak: (outcome, duration) =>
            {
                Console.WriteLine($"[Circuit Breaker] Circuito aberto por {duration.TotalSeconds}s");
            },
            onReset: () =>
            {
                Console.WriteLine("[Circuit Breaker] Circuito fechado (voltou ao normal)");
            },
            onHalfOpen: () =>
            {
                Console.WriteLine("[Circuit Breaker] Circuito meio aberto (testando recuperação)");
            }
        );
}
```

### 4. Diferenciar Erros Transitórios vs Permanentes

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        try
        {
            var resultado = await _servico.ProcessarAsync(
                context.Message.ClienteId,
                context.Message.ValorTotal,
                context.Message.FormaPagamento,
                context.CancellationToken
            );

            // Verificar tipo de erro
            if (resultado.EhFalha)
            {
                var erro = resultado.Erro;

                // Erros que NÃO devem fazer retry (permanentes)
                if (erro.Tipo == TipoErro.Validacao ||
                    erro.Tipo == TipoErro.Negocio ||
                    erro.Tipo == TipoErro.NaoEncontrado)
                {
                    // Enviar resposta de falha imediatamente (sem retry)
                    await context.RespondAsync(new PagamentoProcessado(
                        context.Message.CorrelacaoId,
                        Sucesso: false,
                        TransacaoId: null,
                        MotivoFalha: erro.Mensagem
                    ));
                    return; // NÃO lançar exceção (evitar retry)
                }

                // Erros transitórios (fazer retry)
                if (erro.Tipo == TipoErro.Timeout ||
                    erro.Tipo == TipoErro.Infraestrutura ||
                    erro.Tipo == TipoErro.Externo)
                {
                    // Lançar exceção para acionar retry
                    throw new TransientErrorException(erro.Mensagem);
                }
            }

            // Sucesso
            await context.RespondAsync(new PagamentoProcessado(
                context.Message.CorrelacaoId,
                Sucesso: true,
                TransacaoId: resultado.Valor.TransacaoId,
                MotivoFalha: null
            ));
        }
        catch (TransientErrorException ex)
        {
            // Rebus vai fazer retry automaticamente
            _logger.LogWarning(ex, "Erro transitório ao processar pagamento - Retry será feito");
            throw;
        }
        catch (Exception ex)
        {
            // Erro inesperado - logar e enviar para DLQ após retries
            _logger.LogError(ex, "Erro inesperado ao processar pagamento");
            throw;
        }
    }
}

// Exceção customizada para erros transitórios
public class TransientErrorException : Exception
{
    public TransientErrorException(string message) : base(message) { }
}
```

### 5. Dead Letter Queue (DLQ)

```csharp
// Configurar DLQ no RabbitMQ
services.AddRebus(configure =>
{
    configure
        .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, "saga-queue"))
        .Options(o =>
        {
            o.SimpleRetryStrategy(maxDeliveryAttempts: 5);

            // Configurar Dead Letter Queue
            o.EnableDeadLettering();
            o.SetDeadLetteringQueue("saga-dlq");
        });
});

// Consumer para processar DLQ
public class DeadLetterQueueConsumer : IConsumer<Fault<ProcessarPagamento>>
{
    private readonly ILogger<DeadLetterQueueConsumer> _logger;
    private readonly IAlertaService _alertaService;

    public async Task Consume(ConsumeContext<Fault<ProcessarPagamento>> context)
    {
        var mensagemOriginal = context.Message.Message;
        var excecoes = context.Message.Exceptions;

        _logger.LogError(
            "[DLQ] Mensagem movida para DLQ - CorrelacaoId: {CorrelacaoId}, Tentativas: {Tentativas}",
            mensagemOriginal.CorrelacaoId,
            excecoes.Length
        );

        // Alertar equipe de operações
        await _alertaService.EnviarAlertaAsync(new Alerta
        {
            Severidade = "CRITICAL",
            Mensagem = $"Pedido {mensagemOriginal.CorrelacaoId} movido para DLQ após {excecoes.Length} tentativas",
            Detalhes = string.Join("\n", excecoes.Select(e => e.Message))
        });

        // Salvar em banco para análise posterior
        await SalvarMensagemFalhadaAsync(mensagemOriginal, excecoes);
    }
}
```

### 6. Estratégia de Retry por Tipo de Erro

```csharp
public class RetryPolicyConfig
{
    // Não retry (falha imediata)
    public static readonly HashSet<TipoErro> ErrosNaoRetryaveis = new()
    {
        TipoErro.Validacao,
        TipoErro.Negocio,
        TipoErro.NaoEncontrado
    };

    // Retry com exponential backoff
    public static readonly HashSet<TipoErro> ErrosRetryaveis = new()
    {
        TipoErro.Timeout,
        TipoErro.Infraestrutura,
        TipoErro.Externo
    };

    // Circuit breaker (após N falhas, parar temporariamente)
    public static readonly HashSet<TipoErro> ErrosCircuitBreaker = new()
    {
        TipoErro.Timeout,
        TipoErro.Externo
    };
}
```

## Monitoramento

```csharp
// Métricas de retry
public class RetryMetrics
{
    private static readonly Meter Meter = new("SagaPOC");

    public static readonly Counter<long> RetryAttempts = Meter.CreateCounter<long>(
        "saga.retry.attempts",
        description: "Total de tentativas de retry"
    );

    public static readonly Counter<long> RetrySuccesses = Meter.CreateCounter<long>(
        "saga.retry.successes",
        description: "Total de retries bem-sucedidos"
    );

    public static readonly Counter<long> RetryFailures = Meter.CreateCounter<long>(
        "saga.retry.failures",
        description: "Total de retries que falharam"
    );

    public static readonly Counter<long> CircuitBreakerOpened = Meter.CreateCounter<long>(
        "saga.circuit_breaker.opened",
        description: "Vezes que o circuit breaker foi aberto"
    );

    public static readonly Counter<long> DeadLetterMessages = Meter.CreateCounter<long>(
        "saga.dlq.messages",
        description: "Mensagens enviadas para DLQ"
    );
}
```

## Critérios de Aceitação
- [ ] Retry policy configurado com exponential backoff
- [ ] Circuit Breaker implementado para serviços externos
- [ ] Erros transitórios vs permanentes diferenciados
- [ ] Dead Letter Queue configurado e monitorado
- [ ] Métricas de retry e circuit breaker implementadas
- [ ] Testes de resiliência (network failure, timeout) funcionando
- [ ] Documentação de estratégias de retry atualizada

## Estimativa
**2-3 horas** (configuração + testes)

## Próximos Passos
- [Fase 20 - Idempotência](./fase-20.md)
- [Fase 21 - MongoDB para Auditoria](./fase-21.md)

## Referências
- [Polly Documentation](https://github.com/App-vNext/Polly)
- [Rebus Retry Strategy](https://github.com/rebus-org/Rebus/wiki/Error-handling)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
