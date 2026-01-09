# FASE 20: Idempotência (Deduplicação de Mensagens)


## Objetivos
- Garantir que mensagens duplicadas não sejam processadas múltiplas vezes
- Implementar deduplicação por MessageId
- Prevenir efeitos colaterais duplicados (duplo débito, dupla reserva, etc.)
- Armazenar histórico de mensagens processadas

## Contexto

**Problema**: Retry pode processar a mesma mensagem 2x (duplo débito, dupla reserva, etc.).

**Cenários de Duplicação**:
- Retry após timeout (mensagem foi processada mas resposta não chegou)
- Network split durante envio de mensagem
- Consumer processa mensagem mas falha antes de confirmar (ACK)
- Publicação duplicada por falha no publisher

**Consequências sem Idempotência**:
- Duplo débito em pagamento
- Dupla reserva de estoque
- Múltiplas compensações da mesma transação
- Inconsistência de dados

## Implementação

### 1. Interface de Idempotência

```csharp
public interface IIdempotenciaRepository
{
    Task<bool> JaProcessadoAsync(string messageId, CancellationToken cancellationToken = default);
    Task MarcarProcessadaAsync(string messageId, object resultado, CancellationToken cancellationToken = default);
    Task<T?> ObterResultadoProcessadoAsync<T>(string messageId, CancellationToken cancellationToken = default);
}

public class MensagemProcessada
{
    public string MessageId { get; set; }
    public DateTime DataProcessamento { get; set; }
    public string TipoMensagem { get; set; }
    public string ResultadoSerializado { get; set; } // JSON do resultado
    public DateTime? DataExpiracao { get; set; } // TTL
}
```

### 2. Implementação com Redis

```bash
dotnet add package StackExchange.Redis
```

```csharp
public class RedisIdempotenciaRepository : IIdempotenciaRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisIdempotenciaRepository> _logger;
    private readonly TimeSpan _ttl = TimeSpan.FromDays(7); // Manter por 7 dias

    public RedisIdempotenciaRepository(
        IConnectionMultiplexer redis,
        ILogger<RedisIdempotenciaRepository> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> JaProcessadoAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"idempotencia:{messageId}";

        var existe = await db.KeyExistsAsync(key);

        if (existe)
        {
            _logger.LogWarning(
                "[Idempotência] Mensagem {MessageId} já foi processada (duplicada)",
                messageId
            );
        }

        return existe;
    }

    public async Task MarcarProcessadaAsync(
        string messageId,
        object resultado,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"idempotencia:{messageId}";

        var mensagemProcessada = new MensagemProcessada
        {
            MessageId = messageId,
            DataProcessamento = DateTime.UtcNow,
            TipoMensagem = resultado.GetType().Name,
            ResultadoSerializado = JsonSerializer.Serialize(resultado),
            DataExpiracao = DateTime.UtcNow.Add(_ttl)
        };

        var json = JsonSerializer.Serialize(mensagemProcessada);

        await db.StringSetAsync(key, json, _ttl);

        _logger.LogInformation(
            "[Idempotência] Mensagem {MessageId} marcada como processada (TTL: {TTL})",
            messageId,
            _ttl
        );
    }

    public async Task<T?> ObterResultadoProcessadoAsync<T>(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = $"idempotencia:{messageId}";

        var json = await db.StringGetAsync(key);

        if (!json.HasValue)
            return default;

        var mensagemProcessada = JsonSerializer.Deserialize<MensagemProcessada>(json!);

        return JsonSerializer.Deserialize<T>(mensagemProcessada!.ResultadoSerializado);
    }
}
```

### 3. Implementação com Postgres

```csharp
public class SqlIdempotenciaRepository : IIdempotenciaRepository
{
    private readonly SagaDbContext _dbContext;
    private readonly ILogger<SqlIdempotenciaRepository> _logger;

    public async Task<bool> JaProcessadoAsync(string messageId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MensagensProcessadas
            .AnyAsync(m => m.MessageId == messageId, cancellationToken);
    }

    public async Task MarcarProcessadaAsync(
        string messageId,
        object resultado,
        CancellationToken cancellationToken = default)
    {
        var mensagem = new MensagemProcessada
        {
            MessageId = messageId,
            DataProcessamento = DateTime.UtcNow,
            TipoMensagem = resultado.GetType().Name,
            ResultadoSerializado = JsonSerializer.Serialize(resultado),
            DataExpiracao = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.MensagensProcessadas.Add(mensagem);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<T?> ObterResultadoProcessadoAsync<T>(
        string messageId,
        CancellationToken cancellationToken = default)
    {
        var mensagem = await _dbContext.MensagensProcessadas
            .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

        if (mensagem == null)
            return default;

        return JsonSerializer.Deserialize<T>(mensagem.ResultadoSerializado);
    }
}

// DbContext
public class SagaDbContext : DbContext
{
    public DbSet<MensagemProcessada> MensagensProcessadas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MensagemProcessada>(entity =>
        {
            entity.ToTable("MensagensProcessadas");
            entity.HasKey(e => e.MessageId);
            entity.Property(e => e.MessageId).HasMaxLength(100);
            entity.HasIndex(e => e.DataExpiracao); // Para limpeza
        });
    }
}
```

### 4. Usar Idempotência nos Consumers

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IIdempotenciaRepository _idempotencia;
    private readonly ILogger<ProcessarPagamentoConsumer> _logger;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        var messageId = context.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var correlacaoId = context.Message.CorrelacaoId;

        // 1. Verificar se já processamos esta mensagem
        if (await _idempotencia.JaProcessadoAsync(messageId))
        {
            _logger.LogWarning(
                "[Pagamento] Mensagem {MessageId} já processada (duplicada) - CorrelacaoId: {CorrelacaoId}",
                messageId,
                correlacaoId
            );

            // Retornar resultado armazenado (sem reprocessar)
            var resultadoArmazenado = await _idempotencia
                .ObterResultadoProcessadoAsync<PagamentoProcessado>(messageId);

            if (resultadoArmazenado != null)
            {
                await context.RespondAsync(resultadoArmazenado);
                return;
            }

            // Se não temos resultado armazenado, ignorar (já foi processado)
            return;
        }

        // 2. Processar mensagem normalmente
        var resultado = await _servico.ProcessarAsync(
            context.Message.ClienteId,
            context.Message.ValorTotal,
            context.Message.FormaPagamento,
            context.CancellationToken
        );

        var resposta = new PagamentoProcessado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            TransacaoId: resultado.EhSucesso ? resultado.Valor.TransacaoId : null,
            MotivoFalha: resultado.EhFalha ? resultado.Erro.Mensagem : null
        );

        // 3. Marcar como processada ANTES de enviar resposta
        await _idempotencia.MarcarProcessadaAsync(messageId, resposta);

        // 4. Enviar resposta
        await context.RespondAsync(resposta);

        _logger.LogInformation(
            "[Pagamento] Mensagem {MessageId} processada e marcada como idempotente",
            messageId
        );
    }
}
```

### 5. Limpeza Automática de Mensagens Expiradas

```csharp
public class IdempotenciaCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdempotenciaCleanupWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SagaDbContext>();

                // Limpar mensagens expiradas (> 7 dias)
                var dataLimite = DateTime.UtcNow;
                var mensagensExpiradas = await dbContext.MensagensProcessadas
                    .Where(m => m.DataExpiracao < dataLimite)
                    .ToListAsync(stoppingToken);

                if (mensagensExpiradas.Any())
                {
                    dbContext.MensagensProcessadas.RemoveRange(mensagensExpiradas);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation(
                        "[Idempotência Cleanup] {Count} mensagens expiradas removidas",
                        mensagensExpiradas.Count
                    );
                }

                // Executar a cada 1 hora
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao limpar mensagens expiradas");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}

// Registrar no Program.cs
services.AddHostedService<IdempotenciaCleanupWorker>();
```

### 6. Testes de Idempotência

```csharp
[Fact]
public async Task DeveIgnorarMensagemDuplicada()
{
    // Arrange
    var messageId = Guid.NewGuid().ToString();
    var consumer = CreateConsumer();
    var context = CreateTestContext(messageId);

    // Act - Primeira execução
    await consumer.Consume(context);

    // Act - Segunda execução (duplicada)
    await consumer.Consume(context);

    // Assert
    _servicoPagamento.Verify(
        s => s.ProcessarAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
        Times.Once() // Processado apenas uma vez
    );
}

[Fact]
public async Task DeveRetornarResultadoArmazenadoParaMensagemDuplicada()
{
    // Arrange
    var messageId = Guid.NewGuid().ToString();
    var consumer = CreateConsumer();
    var context = CreateTestContext(messageId);

    // Act - Primeira execução
    await consumer.Consume(context);
    var primeiraResposta = context.SentMessages.Last();

    // Act - Segunda execução (duplicada)
    await consumer.Consume(context);
    var segundaResposta = context.SentMessages.Last();

    // Assert
    Assert.Equal(primeiraResposta, segundaResposta); // Mesma resposta
}
```

## Trade-offs

### Redis
- ✅ **Vantagens**:
  - TTL nativo (limpeza automática)
  - Alta performance
  - Baixa latência
- ❌ **Desvantagens**:
  - Requer backup adequado
  - Custo de memória

### Postgres
- ✅ **Vantagens**:
  - Persistência durável
  - Queries complexas para análise
  - Backup robusto
- ❌ **Desvantagens**:
  - Limpeza manual necessária (background worker)
  - Maior latência

## Recomendação

- **Produção de alta escala**: Redis (performance + TTL automático)
- **Compliance/Auditoria**: Postgres (persistência durável)
- **Híbrido**: Redis para cache (TTL curto) + SQL para auditoria

## Métricas

```csharp
public class IdempotenciaMetrics
{
    private static readonly Meter Meter = new("SagaPOC");

    public static readonly Counter<long> MensagensDuplicadas = Meter.CreateCounter<long>(
        "saga.idempotencia.duplicadas",
        description: "Total de mensagens duplicadas detectadas"
    );

    public static readonly Counter<long> MensagensProcessadas = Meter.CreateCounter<long>(
        "saga.idempotencia.processadas",
        description: "Total de mensagens marcadas como processadas"
    );
}

// Usar ao detectar duplicata
IdempotenciaMetrics.MensagensDuplicadas.Add(1,
    new KeyValuePair<string, object>("tipo_mensagem", typeof(ProcessarPagamento).Name)
);
```

## Critérios de Aceitação
- [ ] Repository de idempotência implementado (Redis ou SQL)
- [ ] Consumers verificam idempotência antes de processar
- [ ] Mensagens duplicadas são ignoradas
- [ ] Resultado armazenado é retornado para duplicatas
- [ ] Limpeza automática de mensagens expiradas funcionando
- [ ] Testes de idempotência passando
- [ ] Métricas de duplicatas implementadas
- [ ] Documentação de idempotência atualizada

## Estimativa
**3-4 horas** (implementação + testes)

## Próximos Passos
- [Fase 21 - MongoDB para Auditoria](./fase-21.md)
- [Próximos Passos - Produção](./proximos-passos.md)

## Referências
- [Idempotency Patterns](https://microservices.io/patterns/data/idempotent-consumer.html)
- [Redis TTL](https://redis.io/commands/expire/)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
