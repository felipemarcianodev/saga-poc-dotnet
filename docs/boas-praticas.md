# Guia de Boas Práticas - SAGA Pattern

Este documento apresenta as boas práticas essenciais para implementar, manter e operar sistemas baseados no padrão SAGA.

---

## Índice

1. [Idempotência](#1-idempotencia)
2. [Compensações](#2-compensacoes)
3. [Timeouts e Resiliência](#3-timeouts-e-resiliencia)
4. [Logs e Observabilidade](#4-logs-e-observabilidade)
5. [Métricas e Monitoramento](#5-metricas-e-monitoramento)
6. [Testes](#6-testes)
7. [Persistência de Estado](#7-persistencia-de-estado)
8. [Mensageria](#8-mensageria)
9. [Tratamento de Erros](#9-tratamento-de-erros)
10. [Performance](#10-performance)

---

## 1. Idempotência

### Por Que é Importante

Em sistemas distribuídos com mensageria, é **inevitável** que mensagens sejam processadas mais de uma vez devido a:
- Retries automáticos
- Falhas de rede
- Rebalanceamento de consumidores
- Timeouts

**Idempotência garante que processar a mesma mensagem N vezes tenha o mesmo resultado de processar 1 vez.**

### ✅ SEMPRE Fazer

#### Verificar Idempotência Antes de Processar

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProcessarPagamentoConsumer> _logger;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        var messageId = context.MessageId!.Value;
        var cacheKey = $"pagamento:{messageId}";

        // 1. SEMPRE verificar primeiro
        var jaProcessado = await _cache.GetStringAsync(cacheKey);
        if (jaProcessado != null)
        {
            _logger.LogWarning(
                "Mensagem duplicada detectada e ignorada: {MessageId}",
                messageId
            );
            return; // Importante: retornar sem erro
        }

        try
        {
            // 2. Processar a mensagem
            var resultado = await _pagamentoService.ProcessarAsync(context.Message);

            // 3. Registrar como processada (TTL de 24-48h)
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(resultado),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
                }
            );

            // 4. Publicar evento de sucesso
            await context.Publish(new PagamentoAprovado
            {
                CorrelacaoId = context.Message.CorrelacaoId,
                TransacaoId = resultado.TransacaoId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar pagamento");
            throw; // Permitir retry
        }
    }
}
```

#### Usar Chaves Únicas Adequadas

```csharp
// ✅ BOM: Usar MessageId do MassTransit
var chave = $"msg:{context.MessageId}";

// ✅ BOM: Usar identificador de negócio
var chave = $"pedido:{context.Message.PedidoId}:validacao";

// ✅ BOM: Combinar múltiplos IDs
var chave = $"estorno:{context.Message.TransacaoId}:{context.Message.ClienteId}";

// ❌ RUIM: Usar CorrelationId sozinho (pode ter múltiplas mensagens com mesmo CorrelationId)
var chave = $"{context.CorrelationId}";
```

#### Definir TTL Apropriado

```csharp
// ✅ BOM: 24-48 horas para operações transacionais
AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)

// ✅ BOM: 7 dias para operações de reconciliação
AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)

// ❌ RUIM: TTL muito curto (aumenta chance de duplicação)
AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)

// ❌ RUIM: Sem TTL (consumo infinito de memória)
// Não definir expiração
```

### ❌ NUNCA Fazer

```csharp
// ❌ NUNCA processar sem verificar idempotência
public async Task Consume(ConsumeContext<ProcessarPagamento> context)
{
    // Perigo: pode cobrar cliente 2x
    await _pagamentoService.CobrarCartaoAsync(context.Message);
}

// ❌ NUNCA confiar apenas em banco de dados
public async Task Consume(ConsumeContext<ProcessarPagamento> context)
{
    // Perigo: race condition entre verificação e inserção
    var existe = await _db.Pedidos.AnyAsync(p => p.Id == context.Message.PedidoId);
    if (!existe)
    {
        await _db.Pedidos.AddAsync(new Pedido { ... });
        await _db.SaveChangesAsync();
    }
}
```

---

## 2. Compensações

### Princípios Fundamentais

**Compensação não é um rollback!** É uma nova transação que desfaz o efeito da transação original.

### ✅ SEMPRE Fazer

#### Compensar em Ordem Reversa

```csharp
public class PedidoSaga : MassTransitStateMachine<EstadoPedido>
{
    public PedidoSaga()
    {
        // Execução: Restaurante → Pagamento → Entregador
        During(AguardandoValidacaoRestaurante,
            When(PedidoValidado)
                .TransitionTo(AguardandoPagamento)
                .PublishAsync(context => context.Init<ProcessarPagamento>(...))
        );

        During(AguardandoPagamento,
            When(PagamentoAprovado)
                .TransitionTo(AguardandoEntregador)
                .PublishAsync(context => context.Init<AlocarEntregador>(...))
        );

        // Compensação: Entregador → Pagamento → Restaurante (ordem reversa)
        During(ExecutandoCompensacao,
            When(CompensacaoIniciada)
                .IfElse(
                    context => context.Saga.EntregadorAlocado,
                    thenActivity => thenActivity
                        .PublishAsync(context => context.Init<CancelarEntregador>(...)),
                    elseActivity => elseActivity
                        .PublishAsync(context => context.Init<EstornarPagamento>(...))
                )
        );
    }
}
```

#### Garantir Idempotência nas Compensações

```csharp
public class EstornarPagamentoConsumer : IConsumer<EstornarPagamento>
{
    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var cacheKey = $"estorno:{context.Message.TransacaoId}";

        // 1. Verificar se já foi estornado
        var jaEstornado = await _cache.GetStringAsync(cacheKey);
        if (jaEstornado != null)
        {
            _logger.LogWarning("Estorno já processado: {TransacaoId}", context.Message.TransacaoId);

            // Publicar evento de sucesso mesmo assim (idempotência)
            await context.Publish(new PagamentoEstornado
            {
                CorrelacaoId = context.Message.CorrelacaoId,
                TransacaoId = context.Message.TransacaoId,
                Idempotente = true
            });
            return;
        }

        // 2. Executar estorno
        await _pagamentoService.EstornarAsync(context.Message.TransacaoId);

        // 3. Registrar como processado
        await _cache.SetStringAsync(cacheKey, "true", new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) // TTL maior para estornos
        });

        // 4. Publicar sucesso
        await context.Publish(new PagamentoEstornado
        {
            CorrelacaoId = context.Message.CorrelacaoId,
            TransacaoId = context.Message.TransacaoId,
            Idempotente = false
        });
    }
}
```

#### Nunca Lançar Exceções em Compensações

```csharp
// ✅ BOM: Compensação robusta
public async Task Consume(ConsumeContext<CancelarPedido> context)
{
    try
    {
        await _restauranteService.CancelarPedidoAsync(context.Message.PedidoId);

        await context.Publish(new PedidoCancelado
        {
            CorrelacaoId = context.Message.CorrelacaoId,
            Sucesso = true
        });
    }
    catch (PedidoNaoEncontradoException ex)
    {
        // Pedido não existe? Considerar como sucesso (já está cancelado)
        _logger.LogWarning(ex, "Pedido não encontrado durante compensação - considerando sucesso");

        await context.Publish(new PedidoCancelado
        {
            CorrelacaoId = context.Message.CorrelacaoId,
            Sucesso = true,
            Motivo = "Pedido não encontrado"
        });
    }
    catch (Exception ex)
    {
        // Outras falhas: permitir retry
        _logger.LogError(ex, "Erro ao compensar pedido");
        throw;
    }
}

// ❌ RUIM: Falhar compensação por motivos triviais
public async Task Consume(ConsumeContext<CancelarPedido> context)
{
    var pedido = await _db.Pedidos.FindAsync(context.Message.PedidoId);
    if (pedido == null)
    {
        throw new Exception("Pedido não encontrado"); // Vai travar a SAGA!
    }
}
```

#### Logar Todas as Compensações

```csharp
public async Task Consume(ConsumeContext<EstornarPagamento> context)
{
    _logger.LogWarning(
        "COMPENSAÇÃO INICIADA: Estornar pagamento {TransacaoId} para pedido {PedidoId}",
        context.Message.TransacaoId,
        context.Message.PedidoId
    );

    try
    {
        await _pagamentoService.EstornarAsync(context.Message.TransacaoId);

        _logger.LogWarning(
            "COMPENSAÇÃO CONCLUÍDA: Estorno {TransacaoId} executado com sucesso",
            context.Message.TransacaoId
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "COMPENSAÇÃO FALHOU: Erro ao estornar {TransacaoId} - Tentativa {Tentativa}",
            context.Message.TransacaoId,
            context.GetRetryAttempt()
        );
        throw;
    }
}
```

### ❌ NUNCA Fazer

```csharp
// ❌ NUNCA assumir que compensação sempre funciona
During(ExecutandoCompensacao,
    When(CompensacaoIniciada)
        .PublishAsync(context => context.Init<EstornarPagamento>(...))
        .TransitionTo(Compensado) // Perigo: e se estorno falhar?
);

// ❌ NUNCA compensar sem idempotência
public async Task Consume(ConsumeContext<CancelarPedido> context)
{
    await _db.Pedidos.DeleteAsync(context.Message.PedidoId); // Pode deletar 2x!
}

// ❌ NUNCA ignorar erros de compensação silenciosamente
catch (Exception ex)
{
    // Log e ignora - PERIGO!
    _logger.LogError(ex, "Erro na compensação");
    // Sistema fica inconsistente
}
```

---

## 3. Timeouts e Resiliência

### ✅ SEMPRE Fazer

#### Definir Timeouts para TODAS as Operações

```csharp
// ✅ Configurar timeout no HttpClient
services.AddHttpClient<IPagamentoService, PagamentoService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5002");
    client.Timeout = TimeSpan.FromSeconds(30); // Timeout do HTTP
})
.AddPolicyHandler(Policy
    .TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(25)) // Timeout do Polly (menor)
);

// ✅ Configurar timeout nas SAGAs
public PedidoSaga()
{
    During(AguardandoPagamento,
        When(PagamentoAprovado)
            .TransitionTo(AguardandoEntregador),

        // Timeout de 2 minutos aguardando resposta
        When(PagamentoTimeout)
            .TransitionTo(ExecutandoCompensacao)
    );

    // Definir evento de timeout
    Event(() => PagamentoTimeout, e => e
        .CorrelateById(context => context.Message.CorrelationId)
    );

    // Agendar timeout
    Schedule(() => TimeoutSchedule, e => e.Delay = TimeSpan.FromMinutes(2));
}
```

#### Usar CancellationToken

```csharp
// ✅ BOM: Propagar CancellationToken
public async Task<Resultado> ProcessarPagamentoAsync(
    ProcessarPagamento comando,
    CancellationToken cancellationToken = default)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(TimeSpan.FromSeconds(30)); // Timeout adicional

    try
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/pagamentos",
            comando,
            cts.Token // Passar token
        );

        return await response.Content.ReadFromJsonAsync<Resultado>(
            cancellationToken: cts.Token
        );
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Timeout ao processar pagamento");
        throw new TimeoutException("Timeout ao processar pagamento");
    }
}
```

#### Configurar Retry com Backoff Exponencial

```csharp
// ✅ Configurar retry no MassTransit
services.AddMassTransit(x =>
{
    x.AddConsumer<ProcessarPagamentoConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.UseMessageRetry(r =>
        {
            r.Exponential(
                retryLimit: 3,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(2)
            );

            // Não fazer retry em erros de validação
            r.Ignore<ValidationException>();
            r.Ignore<ArgumentException>();
        });
    });
});
```

#### Implementar Circuit Breaker

```csharp
// ✅ Configurar circuit breaker com Polly
services.AddHttpClient<IPagamentoService, PagamentoService>()
    .AddTransientHttpErrorPolicy(builder => builder
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(5),
            onBreak: (result, duration) =>
            {
                _logger.LogWarning(
                    "Circuit breaker aberto por {Duration}s após {Failures} falhas",
                    duration.TotalSeconds,
                    5
                );
            },
            onReset: () =>
            {
                _logger.LogInformation("Circuit breaker fechado - serviço recuperado");
            }
        )
    );
```

### ❌ NUNCA Fazer

```csharp
// ❌ NUNCA fazer chamadas HTTP sem timeout
var response = await _httpClient.GetAsync("http://servico-lento.com");

// ❌ NUNCA usar retry infinito
r.Interval(999999, TimeSpan.FromSeconds(1))

// ❌ NUNCA ignorar timeouts
catch (TimeoutException)
{
    // Ignora e continua - PERIGO!
}
```

---

## 4. Logs e Observabilidade

### ✅ SEMPRE Fazer

#### Incluir CorrelationId em TODOS os Logs

```csharp
// ✅ Usar Serilog com enrichers
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Aplicacao", "SagaPoc.Orquestrador")
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss}] [{Level:u3}] [{CorrelacaoId}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

// ✅ Adicionar CorrelationId no contexto
public async Task Consume(ConsumeContext<ProcessarPagamento> context)
{
    using (LogContext.PushProperty("CorrelacaoId", context.CorrelationId))
    using (LogContext.PushProperty("MessageId", context.MessageId))
    {
        _logger.LogInformation("Processando pagamento para pedido {PedidoId}", context.Message.PedidoId);

        // Todos os logs dentro deste escopo terão CorrelationId
    }
}
```

#### Usar Log Estruturado

```csharp
// ✅ BOM: Log estruturado
_logger.LogInformation(
    "Pedido {PedidoId} validado com sucesso - {QuantidadeItens} itens - Valor total: {ValorTotal:C}",
    pedido.Id,
    pedido.Itens.Count,
    pedido.ValorTotal
);

// ❌ RUIM: Log interpolado (não é estruturado)
_logger.LogInformation($"Pedido {pedido.Id} validado - {pedido.Itens.Count} itens");
```

#### Logar Início e Fim de Cada Passo

```csharp
public async Task Consume(ConsumeContext<ProcessarPagamento> context)
{
    using (LogContext.PushProperty("CorrelacaoId", context.CorrelationId))
    {
        _logger.LogInformation("INÍCIO: Processar pagamento {TransacaoId}", context.Message.TransacaoId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var resultado = await _pagamentoService.ProcessarAsync(context.Message);

            stopwatch.Stop();

            _logger.LogInformation(
                "SUCESSO: Pagamento processado em {DuracaoMs}ms - TransacaoId: {TransacaoId}",
                stopwatch.ElapsedMilliseconds,
                resultado.TransacaoId
            );

            await context.Publish(new PagamentoAprovado { ... });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "FALHA: Erro ao processar pagamento após {DuracaoMs}ms",
                stopwatch.ElapsedMilliseconds
            );

            throw;
        }
    }
}
```

### ❌ NUNCA Fazer

```csharp
// ❌ NUNCA logar informações sensíveis
_logger.LogInformation("Processando cartão {NumeroCartao}", cartao.Numero); // PCI violation!

// ❌ NUNCA usar Console.WriteLine em produção
Console.WriteLine($"Processando pedido {pedidoId}");

// ❌ NUNCA logar objetos complexos diretamente
_logger.LogInformation("Pedido: {Pedido}", pedido); // Pode causar referências circulares
```

---

## 5. Métricas e Monitoramento

### ✅ SEMPRE Fazer

#### Coletar Métricas de Taxa de Sucesso/Falha

```csharp
public class MetricasSaga
{
    private readonly IMetrics _metrics;

    public void RegistrarInicioSaga(string tipo)
    {
        _metrics.Measure.Counter.Increment(new CounterOptions
        {
            Name = "saga_iniciadas_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("tipo", tipo)
        });
    }

    public void RegistrarSucesso(TimeSpan duracao, string tipo)
    {
        _metrics.Measure.Counter.Increment(new CounterOptions
        {
            Name = "saga_sucesso_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("tipo", tipo)
        });

        _metrics.Measure.Histogram.Update(new HistogramOptions
        {
            Name = "saga_duracao_segundos",
            MeasurementUnit = Unit.Requests,
            Tags = new MetricTags("tipo", tipo)
        }, (long)duracao.TotalMilliseconds);
    }

    public void RegistrarFalha(string motivo, string tipo)
    {
        _metrics.Measure.Counter.Increment(new CounterOptions
        {
            Name = "saga_falha_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags(new[] { "motivo", "tipo" }, new[] { motivo, tipo })
        });
    }

    public void RegistrarCompensacao(int passos, string tipo)
    {
        _metrics.Measure.Counter.Increment(new CounterOptions
        {
            Name = "saga_compensacoes_total",
            MeasurementUnit = Unit.Calls,
            Tags = new MetricTags("tipo", tipo)
        });

        _metrics.Measure.Histogram.Update(new HistogramOptions
        {
            Name = "saga_compensacao_passos",
            MeasurementUnit = Unit.Items
        }, passos);
    }
}
```

#### Monitorar Duração de Cada Passo

```csharp
public async Task Consume(ConsumeContext<ProcessarPagamento> context)
{
    var stopwatch = Stopwatch.StartNew();

    try
    {
        await _pagamentoService.ProcessarAsync(context.Message);

        _metrics.Measure.Histogram.Update(new HistogramOptions
        {
            Name = "saga_passo_duracao_ms",
            Tags = new MetricTags("passo", "pagamento")
        }, stopwatch.ElapsedMilliseconds);
    }
    catch
    {
        _metrics.Measure.Counter.Increment(new CounterOptions
        {
            Name = "saga_passo_falha_total",
            Tags = new MetricTags("passo", "pagamento")
        });

        throw;
    }
}
```

---

## 6. Testes

### ✅ SEMPRE Fazer

#### Testar TODOS os Cenários de Falha

```csharp
[Fact]
public async Task DeveCompensarQuandoPagamentoFalhar()
{
    // Arrange
    _pagamentoServiceMock
        .Setup(x => x.ProcessarAsync(It.IsAny<ProcessarPagamento>()))
        .ThrowsAsync(new PagamentoRecusadoException("Saldo insuficiente"));

    // Act
    await _harness.Bus.Publish(new IniciarPedido { ... });

    // Assert
    var saga = _harness.Saga<EstadoPedido, PedidoSaga>();
    var instance = saga.Created.ContainsInState(correlacaoId, saga.StateMachine.Compensado);

    Assert.NotNull(instance);
    Assert.True(instance.EmCompensacao);
    Assert.Contains("RestauranteCancelado", instance.PassosCompensados);
}
```

#### Testar Idempotência

```csharp
[Fact]
public async Task DeveSerIdempotente_QuandoProcessarDuasVezes()
{
    // Arrange
    var mensagem = new ProcessarPagamento
    {
        CorrelacaoId = Guid.NewGuid(),
        TransacaoId = "TXN123",
        Valor = 100.00m
    };

    // Act
    await _consumer.Consume(CreateContext(mensagem));
    await _consumer.Consume(CreateContext(mensagem)); // Segunda vez

    // Assert
    _pagamentoServiceMock.Verify(
        x => x.ProcessarAsync(It.IsAny<ProcessarPagamento>()),
        Times.Once // Deve processar apenas 1 vez
    );
}
```

---

## 7. Persistência de Estado

### ✅ SEMPRE Fazer

```csharp
// ✅ Usar índices apropriados
services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
        .MongoDbRepository(r =>
        {
            r.Connection = mongoConnection;
            r.DatabaseName = "saga-poc";
            r.CollectionName = "EstadoPedido";
        });
});

// Criar índices no MongoDB
db.EstadoPedido.createIndex({ CorrelationId: 1 }, { unique: true })
db.EstadoPedido.createIndex({ EstadoAtual: 1, UltimaAtualizacao: -1 })
db.EstadoPedido.createIndex({ ClienteId: 1 })
```

---

## Resumo dos Mandamentos

### Os 10 Mandamentos da SAGA

1. **Idempotência**: Verificar SEMPRE antes de processar
2. **Compensação**: Executar em ordem reversa e de forma idempotente
3. **Timeout**: Definir para TODAS as operações
4. **Logs**: Incluir CorrelationId em TUDO
5. **Métricas**: Coletar sucesso, falha, duração e compensações
6. **Testes**: Cobrir TODOS os cenários de falha
7. **Resiliência**: Usar retry, circuit breaker e fallback
8. **Persistência**: Usar índices e TTL apropriados
9. **Erros**: Nunca ignorar silenciosamente
10. **Simplicidade**: Não over-engineer

---

**Última atualização**: 2026-01-07
**Versão**: 1.0
