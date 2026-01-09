# FASE 10: Tratamentos de Resiliência


#### 3.10.1 Objetivos
- Implementar Retry Policy com exponential backoff
- Configurar Circuit Breaker para proteção de serviços
- Adicionar Timeout Policy
- Configurar Dead Letter Queue
- Implementar Rate Limiting

#### 3.10.2 Entregas

##### 1. **Configuração de Retry Policy no Rebus**

```csharp
// Program.cs - Orquestrador e Serviços
services.AddRebus(x =>
{
    // Configurar consumers
    x.AddConsumer<ValidarPedidoRestauranteConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(configuration["RabbitMQ:Host"]);

        // ============ RETRY POLICY ============
        cfg.UseMessageRetry(retry =>
        {
            // Exponential backoff: 1s, 2s, 4s, 8s, 16s
            retry.Exponential(
                retryLimit: 5,
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(2)
            );

            // Ignorar erros de validação (não adianta retry)
            retry.Ignore<ValidationException>();
            retry.Ignore(e =>
                e.Message.InnerException is InvalidOperationException
            );

            // Retry apenas em erros transitórios
            retry.Handle<TimeoutException>();
            retry.Handle<HttpRequestException>();

            // Incrementar retry count em logs
            retry.OnRetry(retryContext =>
            {
                Console.WriteLine(
                    $"[RETRY] Tentativa {retryContext.RetryAttempt} de {retryContext.RetryCount} - Mensagem: {retryContext.Exception.Message}"
                );
            });
        });

        // ============ CIRCUIT BREAKER ============
        cfg.UseCircuitBreaker(cb =>
        {
            // Abrir circuito após 15 falhas em 1 minuto
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;  // Número de falhas para abrir
            cb.ActiveThreshold = 10; // Tentativas ativas simultâneas

            // Fechar circuito após 5 minutos sem falhas
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });

        // ============ RATE LIMITER ============
        cfg.UseRateLimiter(rl =>
        {
            // Máximo 100 mensagens por segundo
            rl.SetRateLimitForQueue("fila-restaurante", 100, TimeSpan.FromSeconds(1));
            rl.SetRateLimitForQueue("fila-pagamento", 50, TimeSpan.FromSeconds(1));
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

##### 2. **Timeout Policy nos Consumers**

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly ILogger<ProcessarPagamentoConsumer> _logger;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Timeout de 10s
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken,
            cts.Token
        );

        try
        {
            _logger.LogInformation(
                "[Pagamento] Processando pagamento {CorrelacaoId} - Valor: R$ {Valor:F2}",
                context.Message.CorrelacaoId,
                context.Message.ValorTotal
            );

            var resultado = await _servico.ProcessarAsync(
                context.Message.ClienteId,
                context.Message.ValorTotal,
                context.Message.FormaPagamento,
                linkedCts.Token
            );

            resultado.Match(
                sucesso: dados =>
                {
                    _logger.LogInformation(
                        "[Pagamento] Pagamento {CorrelacaoId} aprovado - Transação: {TransacaoId}",
                        context.Message.CorrelacaoId,
                        dados.TransacaoId
                    );
                },
                falha: erro =>
                {
                    _logger.LogWarning(
                        "[Pagamento] Pagamento {CorrelacaoId} recusado - Motivo: {Motivo}",
                        context.Message.CorrelacaoId,
                        erro.Mensagem
                    );
                }
            );

            await context.RespondAsync(new PagamentoProcessado(
                context.Message.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                TransacaoId: resultado.EhSucesso ? resultado.Valor.TransacaoId : null,
                MotivoFalha: resultado.EhFalha ? resultado.Erro.Mensagem : null
            ));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogError(
                "[Pagamento] Timeout ao processar pagamento {CorrelacaoId}",
                context.Message.CorrelacaoId
            );

            await context.RespondAsync(new PagamentoProcessado(
                context.Message.CorrelacaoId,
                Sucesso: false,
                TransacaoId: null,
                MotivoFalha: "Timeout ao processar pagamento"
            ));
        }
    }
}
```

##### 3. **Dead Letter Queue (DLQ) Handling**

```csharp
// Consumer para processar mensagens da DLQ
public class DeadLetterQueueConsumer : IConsumer<Fault<IniciarPedido>>
{
    private readonly ILogger<DeadLetterQueueConsumer> _logger;

    public async Task Consume(ConsumeContext<Fault<IniciarPedido>> context)
    {
        var mensagemOriginal = context.Message.Message;
        var excecoes = context.Message.Exceptions;

        _logger.LogError(
            "[DLQ] Mensagem {MessageId} movida para DLQ após {TentativasRetry} tentativas - Erros: {Erros}",
            context.MessageId,
            excecoes.Length,
            string.Join("; ", excecoes.Select(e => e.Message))
        );

        // Armazenar em banco de dados para análise posterior
        // await _repositorio.SalvarMensagemFalhadaAsync(mensagemOriginal, excecoes);

        // Enviar alerta para equipe de operações
        // await _servicoNotificacao.EnviarAlertaAsync($"Pedido {mensagemOriginal.CorrelacaoId} falhou");
    }
}

// Configuração no Program.cs
x.AddConsumer<DeadLetterQueueConsumer>();

cfg.ReceiveEndpoint("fila-dead-letter", e =>
{
    e.ConfigureConsumer<DeadLetterQueueConsumer>(context);
});
```

##### 4. **Health Checks para Resiliência**

```csharp
// Instalar: dotnet add package AspNetCore.HealthChecks.RabbitMQ
// Instalar: dotnet add package AspNetCore.HealthChecks.UI

// Program.cs
builder.Services.AddHealthChecks()
    .AddCheck("masstransit-bus", () =>
    {
        // Verificar se o bus está conectado
        var busControl = serviceProvider.GetRequiredService<IBusControl>();
        return busControl != null
            ? HealthCheckResult.Healthy("Rebus bus está ativo")
            : HealthCheckResult.Unhealthy("Rebus bus não está respondendo");
    })
    .AddRabbitMQ(
        rabbitConnectionString: "amqp://saga:saga123@localhost:5672",
        name: "rabbitmq-connection",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "rabbitmq", "messaging" }
    )
    .AddCheck<MongoDbHealthCheck>("mongodb", tags: new[] { "database" })
    .AddCheck<CustomSagaHealthCheck>("saga-state", tags: new[] { "saga" });

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var resultado = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsJsonAsync(resultado);
    }
});
```

##### 5. **Polly Integration (Resiliência Avançada)**

```csharp
// Instalar: dotnet add package Polly

public class ServicoPagamentoComPolly : IServicoPagamento
{
    private readonly IAsyncPolicy<Resultado<DadosPagamento>> _politicaResiliencia;

    public ServicoPagamentoComPolly()
    {
        _politicaResiliencia = Policy
            .HandleResult<Resultado<DadosPagamento>>(r =>
                r.EhFalha && r.Erro.Tipo == TipoErro.Timeout
            )
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: tentativa => TimeSpan.FromSeconds(Math.Pow(2, tentativa)),
                onRetry: (resultado, tempo, tentativa, contexto) =>
                {
                    Console.WriteLine($"[Polly] Retry {tentativa} após {tempo.TotalSeconds}s");
                }
            )
            .WrapAsync(
                Policy<Resultado<DadosPagamento>>
                    .Handle<Exception>()
                    .CircuitBreakerAsync(
                        handledEventsAllowedBeforeBreaking: 5,
                        durationOfBreak: TimeSpan.FromMinutes(1),
                        onBreak: (resultado, duracao) =>
                        {
                            Console.WriteLine($"[Polly] Circuit Breaker ABERTO por {duracao.TotalSeconds}s");
                        },
                        onReset: () =>
                        {
                            Console.WriteLine("[Polly] Circuit Breaker FECHADO");
                        }
                    )
            );
    }

    public async Task<Resultado<DadosPagamento>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken = default)
    {
        return await _politicaResiliencia.ExecuteAsync(async () =>
        {
            // Lógica de processamento
            return await ProcessarPagamentoInternoAsync(clienteId, valorTotal, formaPagamento, cancellationToken);
        });
    }
}
```

#### 3.10.3 Critérios de Aceitação
- [ ] Retry policy configurado com exponential backoff
- [ ] Circuit breaker protegendo serviços
- [ ] Timeouts configurados em todos os consumers
- [ ] Dead Letter Queue processando falhas
- [ ] Health checks funcionando
- [ ] Logs mostram tentativas de retry

---

