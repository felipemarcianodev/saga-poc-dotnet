# FASE 16: Persistência do Estado da SAGA


## Objetivos
- Substituir o repositório InMemory por persistência durável
- Garantir que o estado da SAGA sobreviva a reinicializações do orquestrador
- Implementar concorrência otimista para evitar condições de corrida
- Escolher a estratégia de persistência adequada ao ambiente

## Contexto

**Problema**: InMemory repository perde o estado se o orquestrador reiniciar.

**Solução**: Implementar persistência durável usando Postgres

## Implementação

### Opção 1: Postgres (Transacional)

```csharp
// Trocar de:
x.AddRebusSaga<PedidoSaga>()
    .InMemoryRepository();

// Para (Postgres):
x.AddRebusSaga<PedidoSaga>()
    .EntityFrameworkRepository(r =>
    {
        r.ConcurrencyMode = ConcurrencyMode.Optimistic;
        r.AddDbContext<DbContext, SagaDbContext>((provider, builder) =>
        {
            builder.UseNpgsql(connectionString); //
        });
    });
```

**Criação do DbContext**

```csharp
public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    public DbSet<PedidoSagaData> PedidoSagas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PedidoSagaData>(entity =>
        {
            entity.ToTable("PedidoSagas");
            entity.HasKey(e => e.CorrelationId);

            entity.Property(e => e.CorrelationId)
                .HasColumnName("CorrelationId")
                .IsRequired();

            entity.Property(e => e.Revision)
                .IsConcurrencyToken();
        });
    }
}
```

**Migration**

```bash
dotnet ef migrations add AdicionarPersistenciaSaga
dotnet ef database update
```

### Opção 2: Redis (Alta Performance)

```csharp
x.AddRebusSaga<PedidoSaga>()
    .RedisRepository(r =>
    {
        r.DatabaseConfiguration(redisConnectionString);
    });
```

**Configuração do Redis**

```csharp
// appsettings.json
{
  "Redis": {
    "ConnectionString": "localhost:6379,password=saga123"
  }
}

// Program.cs
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});
```

### Opção 3: MongoDB (Schema Flexível)

```csharp
x.AddRebusSaga<PedidoSaga>()
    .MongoDbRepository(r =>
    {
        r.ConnectionString(mongoConnectionString);
        r.DatabaseName("saga_state");
    });
```

## Trade-offs

### Postgres
- **Vantagens**:
  - Suporte transacional ACID completo
  - Melhor para auditoria e consultas complexas
  - Integração nativa com EF Core
  - Backup e recovery bem estabelecidos
- ❌ **Desvantagens**:
  - Performance inferior ao Redis
  - Maior custo de licenciamento (Postgres)
  - Schema rígido

### Redis
- **Vantagens**:
  - Performance extremamente alta
  - Baixa latência
  - Ideal para alta taxa de transações
  - TTL nativo para limpeza automática
- ❌ **Desvantagens**:
  - Requer backup adequado
  - Não suporta queries complexas
  - Memória é mais cara que disco

### MongoDB
- **Vantagens**:
  - Schema flexível
  - Boa performance
  - Bom para evolução de schema
  - Replicação built-in
- ❌ **Desvantagens**:
  - Não usar para dados transacionais críticos
  - Consistência eventual em clusters
  - Menor adoção em ecossistema .NET

## Recomendação

Para ambientes de produção:
- **Alta performance + Caching**: Redis + Postgres (Redis como cache, SQL como source of truth)
- **Transacional crítico**: Postgres
- **Flexibilidade de schema**: MongoDB
- **POC/Dev**: InMemory (atual)

## Configuração de Concorrência

```csharp
// Configurar retry policy para conflitos de concorrência
services.AddRebus(configure =>
{
    configure
        .Options(o =>
        {
            o.OptimisticConcurrencyRetryDelay = TimeSpan.FromMilliseconds(100);
            o.OptimisticConcurrencyRetries = 3;
        })
        .AddRebusSaga<PedidoSaga>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            // ... configuração
        });
});
```

## Monitoramento

```csharp
// Adicionar logging para conflitos de concorrência
public class PedidoSaga : Saga<PedidoSagaData>
{
    private readonly ILogger<PedidoSaga> _logger;

    protected override void CorrelateMessages(ICorrelationConfig<PedidoSagaData> config)
    {
        base.CorrelateMessages(config);

        // Log quando houver conflito de concorrência
        config.OnCorrelationConflict((data, conflictingData) =>
        {
            _logger.LogWarning(
                "Conflito de concorrência detectado - CorrelationId: {CorrelationId}, Revision: {Revision} vs {ConflictingRevision}",
                data.CorrelationId,
                data.Revision,
                conflictingData.Revision
            );
        });
    }
}
```

## Critérios de Aceitação
- [ ] Repositório de persistência configurado (SQL/Redis/MongoDB)
- [ ] Estado da SAGA persiste após reinicialização do orquestrador
- [ ] Concorrência otimista funcionando corretamente
- [ ] Migrations criadas (se Postgres)
- [ ] Testes de persistência criados
- [ ] Documentação de setup atualizada
- [ ] Backup/Recovery documentado

## Estimativa
**2-4 horas** (EF Core setup + migrations + testes)

## Próximos Passos
- [Fase 17 - Outbox Pattern](./fase-17.md)
- [Fase 18 - Observabilidade com OpenTelemetry](./fase-18.md)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
