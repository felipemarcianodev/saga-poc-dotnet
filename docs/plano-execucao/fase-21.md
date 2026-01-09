# FASE 21: MongoDB para Auditoria e Histórico da SAGA


## Objetivos
- Implementar armazenamento de histórico completo de transições da SAGA
- Criar repositório para Dead Letter Queue (DLQ) com análise de falhas
- Implementar Event Sourcing opcional para rebuild de estado
- Criar snapshots de estado da SAGA para recuperação rápida
- Implementar queries de análise e troubleshooting

## Contexto

**Problema**: Precisamos rastrear o histórico completo de transições da SAGA, armazenar mensagens que falharam na DLQ, e criar snapshots para recuperação rápida.

**Por que MongoDB?**
- Schema flexível (ideal para eventos com estrutura variável)
- Queries complexas de análise
- Boa performance para writes (eventos, logs)
- Agregações poderosas para análise

**Casos de Uso**:
1. Dead Letter Queue Storage
2. Histórico de Transições da SAGA
3. Event Sourcing (opcional)
4. Snapshots de Estado

## Implementação

### 1. Dead Letter Queue Storage

```csharp
// Instalar: dotnet add package MongoDB.Driver

public class MensagemFalhadaDocument
{
    public ObjectId Id { get; set; }
    public Guid MessageId { get; set; }
    public string TipoMensagem { get; set; }
    public DateTime DataFalha { get; set; }
    public string ConteudoMensagem { get; set; } // JSON serializado
    public List<ExcecaoInfo> Excecoes { get; set; }
    public int TentativasRetry { get; set; }
    public string CorrelacaoId { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

public class ExcecaoInfo
{
    public string Tipo { get; set; }
    public string Mensagem { get; set; }
    public string StackTrace { get; set; }
    public DateTime Timestamp { get; set; }
}

public interface IDeadLetterQueueRepository
{
    Task SalvarMensagemFalhadaAsync(MensagemFalhadaDocument mensagem);
    Task<List<MensagemFalhadaDocument>> BuscarPorCorrelacaoIdAsync(string correlacaoId);
    Task<List<MensagemFalhadaDocument>> BuscarMensagensRecentes(int ultimasHoras);
}

public class DeadLetterQueueRepository : IDeadLetterQueueRepository
{
    private readonly IMongoCollection<MensagemFalhadaDocument> _collection;

    public DeadLetterQueueRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<MensagemFalhadaDocument>("mensagens_falhas");

        // Criar índices
        var indexKeys = Builders<MensagemFalhadaDocument>.IndexKeys
            .Ascending(x => x.CorrelacaoId)
            .Descending(x => x.DataFalha);
        _collection.Indexes.CreateOne(new CreateIndexModel<MensagemFalhadaDocument>(indexKeys));
    }

    public async Task SalvarMensagemFalhadaAsync(MensagemFalhadaDocument mensagem)
    {
        await _collection.InsertOneAsync(mensagem);
    }

    public async Task<List<MensagemFalhadaDocument>> BuscarPorCorrelacaoIdAsync(string correlacaoId)
    {
        return await _collection
            .Find(x => x.CorrelacaoId == correlacaoId)
            .SortByDescending(x => x.DataFalha)
            .ToListAsync();
    }

    public async Task<List<MensagemFalhadaDocument>> BuscarMensagensRecentes(int ultimasHoras)
    {
        var dataLimite = DateTime.UtcNow.AddHours(-ultimasHoras);
        return await _collection
            .Find(x => x.DataFalha >= dataLimite)
            .SortByDescending(x => x.DataFalha)
            .ToListAsync();
    }
}

// Consumer atualizado
public class DeadLetterQueueConsumer : IConsumer<Fault<IniciarPedido>>
{
    private readonly IDeadLetterQueueRepository _repository;
    private readonly ILogger<DeadLetterQueueConsumer> _logger;

    public async Task Consume(ConsumeContext<Fault<IniciarPedido>> context)
    {
        var mensagemFalhada = new MensagemFalhadaDocument
        {
            MessageId = context.MessageId ?? Guid.NewGuid(),
            TipoMensagem = typeof(IniciarPedido).Name,
            DataFalha = DateTime.UtcNow,
            ConteudoMensagem = JsonSerializer.Serialize(context.Message.Message),
            Excecoes = context.Message.Exceptions.Select(e => new ExcecaoInfo
            {
                Tipo = e.ExceptionType,
                Mensagem = e.Message,
                StackTrace = e.StackTrace,
                Timestamp = e.Timestamp
            }).ToList(),
            TentativasRetry = context.Message.Exceptions.Length,
            CorrelacaoId = context.Message.Message.CorrelacaoId.ToString(),
            Metadata = new Dictionary<string, object>
            {
                ["Host"] = context.Host.MachineName,
                ["ProcessId"] = Environment.ProcessId
            }
        };

        await _repository.SalvarMensagemFalhadaAsync(mensagemFalhada);

        _logger.LogError(
            "[DLQ → MongoDB] Mensagem {MessageId} salva - CorrelacaoId: {CorrelacaoId}",
            mensagemFalhada.MessageId,
            mensagemFalhada.CorrelacaoId
        );
    }
}
```

### 2. Histórico de Transições da SAGA

```csharp
public class TransicaoSagaDocument
{
    public ObjectId Id { get; set; }
    public Guid CorrelacaoId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EstadoAnterior { get; set; }
    public string EstadoNovo { get; set; }
    public string Evento { get; set; }
    public Dictionary<string, object> DadosEvento { get; set; }
    public string Usuario { get; set; } = "sistema";
    public TimeSpan DuracaoTransicao { get; set; }
}

public interface IHistoricoSagaRepository
{
    Task RegistrarTransicaoAsync(TransicaoSagaDocument transicao);
    Task<List<TransicaoSagaDocument>> ObterHistoricoCompletoDaSagaAsync(Guid correlacaoId);
    Task<Dictionary<string, int>> ObterEstatisticasTransicoesAsync(DateTime dataInicio, DateTime dataFim);
}

public class HistoricoSagaRepository : IHistoricoSagaRepository
{
    private readonly IMongoCollection<TransicaoSagaDocument> _collection;

    public HistoricoSagaRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TransicaoSagaDocument>("historico_transicoes");

        // Índices para performance
        _collection.Indexes.CreateOne(new CreateIndexModel<TransicaoSagaDocument>(
            Builders<TransicaoSagaDocument>.IndexKeys.Ascending(x => x.CorrelacaoId)
        ));

        _collection.Indexes.CreateOne(new CreateIndexModel<TransicaoSagaDocument>(
            Builders<TransicaoSagaDocument>.IndexKeys.Descending(x => x.Timestamp)
        ));
    }

    public async Task RegistrarTransicaoAsync(TransicaoSagaDocument transicao)
    {
        await _collection.InsertOneAsync(transicao);
    }

    public async Task<List<TransicaoSagaDocument>> ObterHistoricoCompletoDaSagaAsync(Guid correlacaoId)
    {
        return await _collection
            .Find(x => x.CorrelacaoId == correlacaoId)
            .SortBy(x => x.Timestamp)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> ObterEstatisticasTransicoesAsync(DateTime dataInicio, DateTime dataFim)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "Timestamp", new BsonDocument
                    {
                        { "$gte", dataInicio },
                        { "$lte", dataFim }
                    }
                }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$EstadoNovo" },
                { "count", new BsonDocument("$sum", 1) }
            })
        };

        var resultado = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();

        return resultado.ToDictionary(
            doc => doc["_id"].AsString,
            doc => doc["count"].AsInt32
        );
    }
}

// Integração na State Machine
public class PedidoSaga : Saga<PedidoSagaData>
{
    private readonly IHistoricoSagaRepository _historicoRepository;

    public PedidoSaga(IHistoricoSagaRepository historicoRepository)
    {
        _historicoRepository = historicoRepository;

        Initially(
            When(IniciarPedido)
                .Then(async context =>
                {
                    var transicao = new TransicaoSagaDocument
                    {
                        CorrelacaoId = context.Saga.CorrelationId,
                        Timestamp = DateTime.UtcNow,
                        EstadoAnterior = "Inicial",
                        EstadoNovo = "ValidandoRestaurante",
                        Evento = "IniciarPedido",
                        DadosEvento = new Dictionary<string, object>
                        {
                            ["RestauranteId"] = context.Message.RestauranteId,
                            ["ClienteId"] = context.Message.ClienteId,
                            ["ValorTotal"] = context.Message.Itens.Sum(i => i.PrecoUnitario * i.Quantidade)
                        }
                    };

                    await _historicoRepository.RegistrarTransicaoAsync(transicao);
                })
                .TransitionTo(ValidandoRestaurante)
        );

        // Repetir para todas as transições...
    }
}
```

### 3. Event Sourcing (Opcional)

```csharp
public class EventoSagaDocument
{
    public ObjectId Id { get; set; }
    public Guid CorrelacaoId { get; set; }
    public long SequenceNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public string TipoEvento { get; set; }
    public string DadosEvento { get; set; } // JSON
    public string Versao { get; set; } = "1.0";
}

public interface IEventStoreRepository
{
    Task AdicionarEventoAsync(EventoSagaDocument evento);
    Task<List<EventoSagaDocument>> ObterEventosDaSagaAsync(Guid correlacaoId);
    Task<EstadoPedido> ReconstruirEstadoAsync(Guid correlacaoId);
}
```

### 4. Snapshots de Estado da SAGA

```csharp
public class SnapshotSagaDocument
{
    public ObjectId Id { get; set; }
    public Guid CorrelacaoId { get; set; }
    public DateTime DataSnapshot { get; set; }
    public long UltimaSequencia { get; set; }
    public string EstadoSerializado { get; set; } // JSON do EstadoPedido completo
    public int VersaoSnapshot { get; set; }
}

public interface ISnapshotRepository
{
    Task SalvarSnapshotAsync(SnapshotSagaDocument snapshot);
    Task<SnapshotSagaDocument> ObterUltimoSnapshotAsync(Guid correlacaoId);
}

public class SnapshotRepository : ISnapshotRepository
{
    private readonly IMongoCollection<SnapshotSagaDocument> _collection;

    public SnapshotRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SnapshotSagaDocument>("saga_snapshots");

        // Índice único por CorrelacaoId (manter apenas o último snapshot)
        _collection.Indexes.CreateOne(new CreateIndexModel<SnapshotSagaDocument>(
            Builders<SnapshotSagaDocument>.IndexKeys.Ascending(x => x.CorrelacaoId),
            new CreateIndexOptions { Unique = false }
        ));
    }

    public async Task SalvarSnapshotAsync(SnapshotSagaDocument snapshot)
    {
        await _collection.InsertOneAsync(snapshot);
    }

    public async Task<SnapshotSagaDocument> ObterUltimoSnapshotAsync(Guid correlacaoId)
    {
        return await _collection
            .Find(x => x.CorrelacaoId == correlacaoId)
            .SortByDescending(x => x.DataSnapshot)
            .FirstOrDefaultAsync();
    }
}

// Criar snapshot a cada X transições ou periodicamente
public class SnapshotWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // A cada 1 hora, criar snapshots de SAGAs ativas
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

            // Lógica de snapshot...
        }
    }
}
```

### 5. Configuração do MongoDB

```csharp
// Program.cs
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb")
);

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
    return client.GetDatabase(settings.DatabaseName);
});

builder.Services.AddScoped<IDeadLetterQueueRepository, DeadLetterQueueRepository>();
builder.Services.AddScoped<IHistoricoSagaRepository, HistoricoSagaRepository>();
builder.Services.AddScoped<ISnapshotRepository, SnapshotRepository>();

// appsettings.json
{
  "MongoDb": {
    "ConnectionString": "mongodb://saga:saga123@localhost:27017",
    "DatabaseName": "saga_auditoria"
  }
}
```

### 6. Docker Compose - MongoDB

```yaml
mongodb:
  image: mongo:7.0
  container_name: saga-mongodb
  environment:
    MONGO_INITDB_ROOT_USERNAME: saga
    MONGO_INITDB_ROOT_PASSWORD: saga123
    MONGO_INITDB_DATABASE: saga_auditoria
  volumes:
    - mongodb-data:/data/db
    - mongodb-config:/data/configdb
  networks:
    - saga-network
  ports:
    - "27017:27017"
  healthcheck:
    test: ["CMD", "mongosh", "--eval", "db.adminCommand('ping')"]
    interval: 10s
    timeout: 5s
    retries: 5

mongo-express:
  image: mongo-express:latest
  container_name: saga-mongo-express
  environment:
    ME_CONFIG_MONGODB_ADMINUSERNAME: saga
    ME_CONFIG_MONGODB_ADMINPASSWORD: saga123
    ME_CONFIG_MONGODB_URL: mongodb://saga:saga123@mongodb:27017/
  networks:
    - saga-network
  ports:
    - "8081:8081"
  depends_on:
    mongodb:
      condition: service_healthy

volumes:
  mongodb-data:
    driver: local
  mongodb-config:
    driver: local
```

### 7. Queries de Análise Úteis

```csharp
// 1. Taxa de sucesso vs falha por período
public async Task<Dictionary<string, int>> ObterTaxaSucessoFalhaAsync(DateTime inicio, DateTime fim)
{
    var pipeline = new[]
    {
        new BsonDocument("$match", new BsonDocument("Timestamp", new BsonDocument
        {
            { "$gte", inicio },
            { "$lte", fim }
        })),
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$EstadoNovo" },
            { "count", new BsonDocument("$sum", 1) }
        })
    };

    // Executar pipeline...
}

// 2. Top 10 erros mais frequentes na DLQ
public async Task<List<(string Erro, int Ocorrencias)>> ObterTopErrosAsync()
{
    var pipeline = new[]
    {
        new BsonDocument("$unwind", "$Excecoes"),
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$Excecoes.Mensagem" },
            { "count", new BsonDocument("$sum", 1) }
        }),
        new BsonDocument("$sort", new BsonDocument("count", -1)),
        new BsonDocument("$limit", 10)
    };

    // Executar pipeline...
}

// 3. Duração média das SAGAs por estado
public async Task<Dictionary<string, double>> ObterDuracaoMediaPorEstadoAsync()
{
    var pipeline = new[]
    {
        new BsonDocument("$group", new BsonDocument
        {
            { "_id", "$EstadoNovo" },
            { "duracaoMedia", new BsonDocument("$avg", "$DuracaoTransicao") }
        })
    };

    // Executar pipeline...
}
```

## Benefícios

- ✅ Rastreabilidade completa de todas transições da SAGA
- ✅ Análise de mensagens que falharam sem perder contexto
- ✅ Event Sourcing para rebuild de estado
- ✅ Snapshots para recuperação rápida após crash
- ✅ Queries complexas para troubleshooting e análise de negócio

## Trade-offs

- **Benefício**: Schema flexível, perfeito para logs e eventos com estrutura variável
- **Atenção**: Não usar para dados transacionais (use PostgreSQL)
- **Performance**: Criar índices adequados para queries frequentes

## Critérios de Aceitação
- [ ] MongoDB configurado e rodando via Docker
- [ ] Repositório de DLQ implementado e funcionando
- [ ] Histórico de transições da SAGA sendo registrado
- [ ] Snapshots sendo criados periodicamente
- [ ] Queries de análise implementadas
- [ ] Mongo Express acessível para visualização
- [ ] Índices criados para performance
- [ ] Documentação de schemas e queries atualizada

## Estimativa
**6-10 horas** (setup + implementação dos 4 casos de uso)

## Próximos Passos
- [Próximos Passos - Produção](./proximos-passos.md)

## Referências
- [MongoDB .NET Driver](https://www.mongodb.com/docs/drivers/csharp/)
- [Event Sourcing Pattern](https://microservices.io/patterns/data/event-sourcing.html)

---

[← Voltar ao Plano de Execução](./plano-execucao.md)
