# FASE 17: Outbox Pattern (Garantias Transacionais)


## Objetivos
- Implementar Outbox Pattern para garantir atomicidade entre banco de dados e mensageria
- Garantir que mensagens sejam enviadas APENAS se a transação commitou
- Evitar perda de mensagens ou mensagens duplicadas
- Implementar retry automático de mensagens que falharam

## Contexto

**Problema**: Mensagem publicada mas transação no banco falha (ou vice-versa).

**Cenário de Falha**:
```
1. Salvar estado da SAGA no banco → Sucesso
2. Publicar mensagem no RabbitMQ → ❌ Falha (network issue)
Resultado: Estado inconsistente (banco atualizado, mas mensagem não enviada)

OU

1. Publicar mensagem no RabbitMQ → Sucesso
2. Salvar estado da SAGA no banco → ❌ Falha (constraint violation)
Resultado: Mensagem enviada mas estado não persistido
```

**Solução**: Implementar Outbox Pattern com Rebus

## Implementação

### 1. Configurar Outbox no DbContext

```csharp
services.AddDbContext<SagaDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.AddRebusOutbox(); // Adicionar suporte a Outbox
});
```

### 2. Configurar Outbox no Rebus

```csharp
services.AddRebus(x =>
{
    x.AddEntityFrameworkOutbox<SagaDbContext>(o =>
    {
        o.UseNpgsql(); // Usar Postgres para Outbox
        o.UseBusOutbox(); // Usar outbox para publicações do bus
    });
});
```

### 3. Atualizar DbContext

```csharp
public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    public DbSet<PedidoSagaData> PedidoSagas { get; set; }

    // Tabelas do Outbox (criadas automaticamente pelo Rebus)
    // - OutboxMessages: Mensagens pendentes de envio
    // - OutboxProcessedMessages: Mensagens já processadas (deduplicação)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configurar Outbox tables
        modelBuilder.ApplyConfiguration(new RebusOutboxConfiguration());
    }
}
```

### 4. Usar Outbox nos Consumers

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly SagaDbContext _dbContext;
    private readonly IBus _bus;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        // Iniciar transação
        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            // 1. Processar pagamento
            var resultado = await _servico.ProcessarAsync(...);

            // 2. Salvar estado no banco
            var sagaData = await _dbContext.PedidoSagas
                .FindAsync(context.Message.CorrelacaoId);

            sagaData.TransacaoId = resultado.TransacaoId;
            await _dbContext.SaveChangesAsync();

            // 3. Publicar mensagem (vai para Outbox, não diretamente para RabbitMQ)
            await _bus.Publish(new PagamentoProcessado(
                context.Message.CorrelacaoId,
                resultado.TransacaoId
            ));

            // 4. Commit da transação (inclui Outbox)
            await transaction.CommitAsync();

            // Outbox worker vai processar mensagens pendentes em background
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
```

### 5. Configurar Outbox Worker

```csharp
// Program.cs
services.AddHostedService<OutboxWorker>();

public class OutboxWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
                var bus = scope.ServiceProvider.GetRequiredService<IBus>();

                // Processar mensagens pendentes no Outbox
                await bus.Advanced.OutboxProcessor.ProcessPendingMessages();

                // Aguardar intervalo configurável (ex: 5 segundos)
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar Outbox");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
```

## Como Funciona

1. **Publish**: Mensagem é salva na tabela `OutboxMessages` (dentro da transação do banco)
2. **Commit**: Transação commita (banco + outbox juntos)
3. **Background Worker**: Processa mensagens pendentes do Outbox e envia para RabbitMQ
4. **Deduplicação**: Mensagens processadas vão para `OutboxProcessedMessages` (evita duplicatas)

## Benefícios

- Garante que mensagens são enviadas APENAS se a transação commitou
- Retry automático de mensagens que falharam
- Histórico de mensagens enviadas (auditoria)
- Deduplicação de mensagens
- Consistência transacional entre banco e mensageria

## Configuração Avançada

### 1. Configurar Intervalo de Processamento

```csharp
services.AddRebus(x =>
{
    x.AddEntityFrameworkOutbox<SagaDbContext>(o =>
    {
        o.UseNpgsql();
        o.UseBusOutbox();
        o.ProcessInterval = TimeSpan.FromSeconds(5); // Processar a cada 5s
        o.MaxMessagesPerBatch = 100; // Processar até 100 mensagens por vez
    });
});
```

### 2. Configurar Retenção de Mensagens Processadas

```csharp
services.AddRebus(x =>
{
    x.AddEntityFrameworkOutbox<SagaDbContext>(o =>
    {
        o.UseNpgsql();
        o.UseBusOutbox();
        o.MessageRetention = TimeSpan.FromDays(7); // Manter histórico por 7 dias
    });
});
```

### 3. Monitoramento

```csharp
public class OutboxMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxMonitor> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SagaDbContext>();

            // Contar mensagens pendentes
            var pendingCount = await dbContext.Set<OutboxMessage>()
                .CountAsync(m => !m.Processed, stoppingToken);

            if (pendingCount > 100)
            {
                _logger.LogWarning(
                    "Alto volume de mensagens pendentes no Outbox: {Count}",
                    pendingCount
                );
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## Migrations

```bash
# Criar migration para tabelas do Outbox
dotnet ef migrations add AdicionarOutboxPattern
dotnet ef database update
```

## Testes

```csharp
[Fact]
public async Task DeveSalvarMensagemNoOutboxQuandoTransacaoCommitar()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    var bus = CreateTestBus();

    // Act
    using (var transaction = await dbContext.Database.BeginTransactionAsync())
    {
        var sagaData = new PedidoSagaData { CorrelationId = Guid.NewGuid() };
        dbContext.PedidoSagas.Add(sagaData);
        await dbContext.SaveChangesAsync();

        await bus.Publish(new PagamentoProcessado(sagaData.CorrelationId, "TXN123"));

        await transaction.CommitAsync();
    }

    // Assert
    var outboxMessages = await dbContext.Set<OutboxMessage>().ToListAsync();
    Assert.Single(outboxMessages);
    Assert.False(outboxMessages[0].Processed);
}

[Fact]
public async Task NaoDeveSalvarMensagemNoOutboxQuandoTransacaoRollback()
{
    // Arrange
    var dbContext = CreateInMemoryDbContext();
    var bus = CreateTestBus();

    // Act
    using (var transaction = await dbContext.Database.BeginTransactionAsync())
    {
        var sagaData = new PedidoSagaData { CorrelationId = Guid.NewGuid() };
        dbContext.PedidoSagas.Add(sagaData);
        await dbContext.SaveChangesAsync();

        await bus.Publish(new PagamentoProcessado(sagaData.CorrelationId, "TXN123"));

        await transaction.RollbackAsync(); // ROLLBACK
    }

    // Assert
    var outboxMessages = await dbContext.Set<OutboxMessage>().ToListAsync();
    Assert.Empty(outboxMessages); // Não deve ter mensagens
}
```

## Critérios de Aceitação
- [ ] Outbox Pattern configurado no DbContext
- [ ] Tabelas do Outbox criadas (migrations)
- [ ] Outbox Worker processando mensagens em background
- [ ] Testes de transação + outbox funcionando
- [ ] Monitoramento de mensagens pendentes implementado
- [ ] Deduplicação de mensagens funcionando
- [ ] Documentação de arquitetura atualizada

## Estimativa
**4-8 horas** (setup + implementação + testes)

## Referências
- [Rebus Outbox Documentation](https://github.com/rebus-org/Rebus/wiki/Outbox)
- [Outbox Pattern Explained](https://microservices.io/patterns/data/transactional-outbox.html)

## Próximos Passos
- [Fase 18 - Observabilidade com OpenTelemetry](./fase-18.md)
- [Fase 19 - Retry Policy e Circuit Breaker](./fase-19.md)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
