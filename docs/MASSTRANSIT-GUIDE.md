# Guia MassTransit - SAGA State Machine

Este documento explica como o MassTransit funciona nesta POC, desde a configuração até padrões avançados.

---

## 📚 O que é MassTransit?

**MassTransit** é um framework open-source para .NET que abstrai a complexidade de mensageria distribuída.

### Principais Recursos

| Recurso | Descrição |
|---------|-----------|
| **Abstração de Transport** | Suporta RabbitMQ, Azure Service Bus, Kafka, Amazon SQS |
| **State Machine (SAGA)** | Orquestração de transações distribuídas |
| **Request/Response** | Comunicação síncrona sobre infraestrutura assíncrona |
| **Retry/Circuit Breaker** | Resiliência embutida |
| **Outbox Pattern** | Garantia de entrega com transações |
| **Observabilidade** | Integração com OpenTelemetry, Application Insights |

---

## 🚀 Instalação e Configuração

### 1. Pacotes NuGet

```xml
<!-- Todos os projetos -->
<PackageReference Include="MassTransit" Version="8.1.3" />
<PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.1.3" />

<!-- Apenas Orquestrador (State Machine) -->
<PackageReference Include="MassTransit.StateMachine" Version="8.1.3" />

<!-- Apenas API (Request Client) -->
<PackageReference Include="MassTransit.AspNetCore" Version="8.1.3" />
```

### 2. Configuração Básica (Serviço)

**Exemplo: SagaPoc.ServicoRestaurante**

```csharp
using MassTransit;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Registrar serviços de domínio
        services.AddScoped<IServicoRestaurante, ServicoRestaurante>();

        // Configurar MassTransit
        services.AddMassTransit(x =>
        {
            // Registrar Consumers
            x.AddConsumer<ValidarPedidoRestauranteConsumer>();
            x.AddConsumer<CancelarPedidoRestauranteConsumer>();

            // Configurar Azure Service Bus
            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(context.Configuration["AzureServiceBus:ConnectionString"]);

                // Configurar endpoint (fila) para este serviço
                cfg.ReceiveEndpoint("fila-restaurante", e =>
                {
                    e.ConfigureConsumer<ValidarPedidoRestauranteConsumer>(context);
                    e.ConfigureConsumer<CancelarPedidoRestauranteConsumer>(context);
                });
            });
        });
    });
```

### 3. Configuração do Orquestrador (State Machine)

**Exemplo: SagaPoc.Orquestrador**

```csharp
services.AddMassTransit(x =>
{
    // Registrar State Machine e estado da SAGA
    x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
        .InMemoryRepository(); // ⚠️ POC apenas - usar SQL/Redis em produção

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(configuration["AzureServiceBus:ConnectionString"]);

        // ConfigureEndpoints cria automaticamente a fila da SAGA
        cfg.ConfigureEndpoints(context);
    });
});
```

### 4. Configuração na API (Publish Endpoint)

**Exemplo: SagaPoc.Api**

```csharp
services.AddMassTransit(x =>
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(configuration["AzureServiceBus:ConnectionString"]);
    });
});

// Registrar IPublishEndpoint no DI
// (MassTransit já faz isso automaticamente)
```

---

## 🎯 Consumers (Consumidores de Mensagens)

### O que é um Consumer?

Um **Consumer** é uma classe que processa mensagens de uma fila.

### Exemplo Completo

```csharp
using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;

public class ValidarPedidoRestauranteConsumer : IConsumer<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly ILogger<ValidarPedidoRestauranteConsumer> _logger;

    public ValidarPedidoRestauranteConsumer(
        IServicoRestaurante servico,
        ILogger<ValidarPedidoRestauranteConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
    {
        _logger.LogInformation(
            "Validando pedido no restaurante {RestauranteId} com {Quantidade} itens. CorrelationId: {CorrelationId}",
            context.Message.RestauranteId,
            context.Message.Itens.Count,
            context.Message.CorrelacaoId
        );

        // Processar validação
        var resultado = await _servico.ValidarPedidoAsync(
            context.Message.RestauranteId,
            context.Message.Itens
        );

        // Responder à SAGA
        await context.RespondAsync(new PedidoRestauranteValidado(
            CorrelacaoId: context.Message.CorrelacaoId,
            Valido: resultado.EhSucesso,
            ValorTotal: resultado.EhSucesso ? resultado.Valor.ValorTotal : 0,
            TempoPreparoMinutos: resultado.EhSucesso ? resultado.Valor.TempoPreparoMinutos : 0,
            MotivoRejeicao: resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));

        if (resultado.EhSucesso)
        {
            _logger.LogInformation(
                "Pedido validado com sucesso. ValorTotal: R$ {Valor}, TempoPreparo: {Tempo}min",
                resultado.Valor.ValorTotal,
                resultado.Valor.TempoPreparoMinutos
            );
        }
        else
        {
            _logger.LogWarning(
                "Pedido rejeitado. Motivo: {Motivo}",
                resultado.Erro.Mensagem
            );
        }
    }
}
```

### Injeção de Dependências

Os Consumers são registrados no DI automaticamente pelo MassTransit:

```csharp
x.AddConsumer<ValidarPedidoRestauranteConsumer>();
```

Você pode injetar qualquer serviço registrado no DI:
- `ILogger<T>`
- Serviços de domínio (`IServicoRestaurante`)
- Repositórios, DbContext, etc.

---

## 🤖 State Machine (SAGA)

### O que é uma State Machine?

Uma **State Machine** define:
- **Estados** possíveis da SAGA
- **Eventos** que causam transições
- **Ações** a serem executadas em cada transição

### Estrutura da State Machine

```csharp
using MassTransit;
using SagaPoc.Shared.Mensagens;

public class PedidoSaga : MassTransitStateMachine<EstadoPedido>
{
    // ========== ESTADOS ==========
    public State ValidandoRestaurante { get; private set; }
    public State ProcessandoPagamento { get; private set; }
    public State AlocandoEntregador { get; private set; }
    public State NotificandoCliente { get; private set; }
    public State PedidoConfirmado { get; private set; }
    public State PedidoCancelado { get; private set; }

    // ========== EVENTOS ==========
    public Event<IniciarPedido> IniciarPedido { get; private set; }
    public Event<PedidoRestauranteValidado> PedidoValidado { get; private set; }
    public Event<PagamentoProcessado> PagamentoProcessado { get; private set; }
    public Event<EntregadorAlocado> EntregadorAlocado { get; private set; }
    public Event<NotificacaoEnviada> NotificacaoEnviada { get; private set; }

    public PedidoSaga()
    {
        // Definir propriedade que armazena o estado atual
        InstanceState(x => x.EstadoAtual);

        // ========== ESTADO INICIAL ==========
        Initially(
            When(IniciarPedido)
                .Then(context =>
                {
                    // Inicializar dados da SAGA
                    context.Saga.ClienteId = context.Message.ClienteId;
                    context.Saga.RestauranteId = context.Message.RestauranteId;
                    context.Saga.EnderecoEntrega = context.Message.EnderecoEntrega;
                    context.Saga.DataInicio = DateTime.UtcNow;

                    context.Saga.ValorTotal = context.Message.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
                })
                .TransitionTo(ValidandoRestaurante)
                .Publish(context => new ValidarPedidoRestaurante(
                    context.Saga.CorrelationId,
                    context.Message.RestauranteId,
                    context.Message.Itens
                ))
        );

        // ========== VALIDANDO RESTAURANTE ==========
        During(ValidandoRestaurante,
            When(PedidoValidado)
                .IfElse(
                    context => context.Message.Valido,
                    // SE VÁLIDO:
                    valido => valido
                        .Then(context =>
                        {
                            context.Saga.ValorTotal = context.Message.ValorTotal;
                            context.Saga.TempoPreparoMinutos = context.Message.TempoPreparoMinutos;
                        })
                        .TransitionTo(ProcessandoPagamento)
                        .Publish(context => new ProcessarPagamento(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            context.Saga.ValorTotal,
                            context.Data.FormaPagamento // ⚠️ Acesso ao evento inicial
                        )),
                    // SE INVÁLIDO:
                    invalido => invalido
                        .TransitionTo(PedidoCancelado)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido cancelado: {context.Message.MotivoRejeicao}",
                            TipoNotificacao.PedidoCancelado
                        ))
                        .Finalize()
                )
        );

        // ========== PROCESSANDO PAGAMENTO ==========
        During(ProcessandoPagamento,
            When(PagamentoProcessado)
                .IfElse(
                    context => context.Message.Sucesso,
                    // SE SUCESSO:
                    sucesso => sucesso
                        .Then(context =>
                        {
                            context.Saga.TransacaoId = context.Message.TransacaoId;
                        })
                        .TransitionTo(AlocandoEntregador)
                        .Publish(context => new AlocarEntregador(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.EnderecoEntrega,
                            context.Saga.ValorTotal * 0.15m // Taxa de 15%
                        )),
                    // SE FALHA: COMPENSAR
                    falha => falha
                        // ⬅️ COMPENSAÇÃO: Cancelar pedido no restaurante
                        .Publish(context => new CancelarPedidoRestaurante(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.PedidoRestauranteId!.Value
                        ))
                        .TransitionTo(PedidoCancelado)
                        .Finalize()
                )
        );

        // ========== ALOCANDO ENTREGADOR ==========
        During(AlocandoEntregador,
            When(EntregadorAlocado)
                .IfElse(
                    context => context.Message.Alocado,
                    // SE ALOCADO:
                    alocado => alocado
                        .Then(context =>
                        {
                            context.Saga.EntregadorId = context.Message.EntregadorId;
                            context.Saga.TempoEntregaMinutos = context.Message.TempoEstimadoMinutos;
                        })
                        .TransitionTo(NotificandoCliente)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido confirmado! Entregador {context.Message.EntregadorId} alocado. " +
                            $"Tempo estimado: {context.Saga.TempoPreparoMinutos + context.Saga.TempoEntregaMinutos}min",
                            TipoNotificacao.PedidoConfirmado
                        )),
                    // SE SEM ENTREGADOR: COMPENSAR EM CASCATA
                    semEntregador => semEntregador
                        // ⬅️ COMPENSAÇÃO 1: Estornar pagamento
                        .Publish(context => new EstornarPagamento(
                            context.Saga.CorrelationId,
                            context.Saga.TransacaoId!
                        ))
                        // ⬅️ COMPENSAÇÃO 2: Cancelar pedido no restaurante
                        .Publish(context => new CancelarPedidoRestaurante(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.PedidoRestauranteId!.Value
                        ))
                        .TransitionTo(PedidoCancelado)
                        .Finalize()
                )
        );

        // ========== NOTIFICANDO CLIENTE ==========
        During(NotificandoCliente,
            When(NotificacaoEnviada)
                .Then(context =>
                {
                    context.Saga.DataConclusao = DateTime.UtcNow;
                })
                .TransitionTo(PedidoConfirmado)
                .Finalize()
        );

        // ========== ESTADOS FINAIS ==========
        SetCompletedWhenFinalized();
    }
}
```

### Estado da SAGA (Instance)

```csharp
using MassTransit;

public class EstadoPedido : SagaStateMachineInstance
{
    // Chave primária (obrigatória)
    public Guid CorrelationId { get; set; }

    // Estado atual (obrigatório)
    public string EstadoAtual { get; set; }

    // Dados do Pedido
    public string ClienteId { get; set; }
    public string RestauranteId { get; set; }
    public decimal ValorTotal { get; set; }
    public string EnderecoEntrega { get; set; }
    public string FormaPagamento { get; set; }

    // Controle de Compensação
    public string? TransacaoId { get; set; }
    public string? EntregadorId { get; set; }
    public Guid? PedidoRestauranteId { get; set; }

    // Métricas
    public int TempoPreparoMinutos { get; set; }
    public int TempoEntregaMinutos { get; set; }

    // Timestamps
    public DateTime DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }
}
```

---

## 🔄 Padrões de Comunicação

### 1. **Publish (Fire-and-Forget)**

Enviar mensagem sem esperar resposta.

**Quando usar**: Eventos de domínio, notificações.

```csharp
// Na API:
await _publishEndpoint.Publish(new IniciarPedido(
    Guid.NewGuid(),
    "CLI001",
    "REST001",
    itens,
    endereco,
    "CREDITO"
));
```

```csharp
// Na State Machine:
.Publish(context => new ValidarPedidoRestaurante(
    context.Saga.CorrelationId,
    context.Message.RestauranteId,
    context.Message.Itens
))
```

### 2. **Request/Response**

Enviar mensagem e esperar resposta.

**Quando usar**: Consultas, operações síncronas.

```csharp
// Configurar Request Client no DI:
services.AddScoped<IRequestClient<ConsultarStatusPedido>>();

// Usar no Controller:
var response = await _requestClient.GetResponse<StatusPedidoResponse>(
    new ConsultarStatusPedido(pedidoId)
);

return Ok(response.Message);
```

### 3. **RespondAsync (no Consumer)**

Responder a uma requisição.

```csharp
public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
{
    var resultado = await _servico.ValidarPedidoAsync(...);

    await context.RespondAsync(new PedidoRestauranteValidado(
        context.Message.CorrelacaoId,
        resultado.EhSucesso,
        // ...
    ));
}
```

---

## 🔁 Correlação de Mensagens

### O que é Correlação?

**Correlação** permite que o MassTransit saiba qual instância da SAGA deve processar cada mensagem.

### CorrelationId vs MessageId

| Campo | Descrição | Quem define |
|-------|-----------|-------------|
| **CorrelationId** | ID da SAGA (mesmo para todas as mensagens) | Aplicação |
| **MessageId** | ID único de cada mensagem | MassTransit |

### Configuração

```csharp
// Na mensagem:
public record IniciarPedido(
    Guid CorrelacaoId,  // ← Este é o CorrelationId
    string ClienteId,
    // ...
);

// Na State Machine:
Event(() => IniciarPedido, x => x.CorrelateById(m => m.Message.CorrelacaoId));
Event(() => PedidoValidado, x => x.CorrelateById(m => m.Message.CorrelacaoId));
```

**Como funciona**:
1. API cria `CorrelationId = Guid.NewGuid()`
2. Publica `IniciarPedido` com este ID
3. State Machine cria instância da SAGA com `CorrelationId`
4. Todas as mensagens posteriores usam o mesmo `CorrelationId`
5. MassTransit roteia mensagens para a instância correta

---

## 📊 Persistência da SAGA

### InMemory (POC)

```csharp
x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
    .InMemoryRepository();
```

**Prós**:
- ✅ Zero configuração
- ✅ Rápido para testes

**Contras**:
- ❌ Perde estado ao reiniciar
- ❌ Não escala (single instance)

### Entity Framework (Produção)

```csharp
// 1. Criar DbContext:
public class SagaDbContext : DbContext
{
    public DbSet<EstadoPedido> EstadosPedido { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EstadoPedidoMap());
    }
}

// 2. Configurar no MassTransit:
x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
    .EntityFrameworkRepository(r =>
    {
        r.ConcurrencyMode = ConcurrencyMode.Optimistic;
        r.AddDbContext<DbContext, SagaDbContext>((provider, builder) =>
        {
            builder.UseSqlServer(connectionString);
        });
    });

// 3. Criar Migration:
dotnet ef migrations add InitialSaga --project SagaPoc.Orquestrador
dotnet ef database update --project SagaPoc.Orquestrador
```

### Redis (Produção - Alta Performance)

```csharp
x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
    .RedisRepository(r =>
    {
        r.DatabaseConfiguration("localhost:6379");
        r.KeyPrefix = "saga:pedido";
    });
```

---

## 🛡️ Resiliência

### 1. Retry Policy

```csharp
x.UsingAzureServiceBus((context, cfg) =>
{
    cfg.Host(connectionString);

    cfg.UseMessageRetry(r =>
    {
        // Exponential backoff: 1s, 6s, 16s, 30s, 30s
        r.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(5)
        );

        // Não fazer retry em erros de validação:
        r.Ignore<ValidationException>();
        r.Ignore<ArgumentException>();
    });

    cfg.ConfigureEndpoints(context);
});
```

### 2. Circuit Breaker

```csharp
cfg.UseCircuitBreaker(cb =>
{
    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
    cb.TripThreshold = 15;      // Abre após 15 falhas em 1min
    cb.ActiveThreshold = 10;     // Fecha após 10 sucessos
    cb.ResetInterval = TimeSpan.FromMinutes(5);
});
```

### 3. Rate Limiting

```csharp
cfg.UseRateLimit(1000, TimeSpan.FromSeconds(1)); // 1000 msg/s
```

---

## 📈 Observabilidade

### Logging

```csharp
cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(prefix: "saga-poc", includeNamespace: false));

// Habilitar logs do MassTransit:
builder.Logging.AddFilter("MassTransit", LogLevel.Information);
```

### Telemetria (OpenTelemetry)

```csharp
// Instalar pacotes:
// MassTransit.Extensions.DependencyInjection
// OpenTelemetry.Instrumentation.AspNetCore
// Azure.Monitor.OpenTelemetry.Exporter

services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddSource("MassTransit")
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
            });
    });
```

---

## 🧪 Testes

### Testar Consumer

```csharp
using MassTransit.Testing;
using Xunit;

public class ValidarPedidoRestauranteConsumerTests
{
    [Fact]
    public async Task DeveValidarPedido_QuandoRestauranteAberto()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<ValidarPedidoRestauranteConsumer>();
            })
            .AddScoped<IServicoRestaurante, ServicoRestauranteFake>()
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Act
        await harness.Bus.Publish(new ValidarPedidoRestaurante(
            Guid.NewGuid(),
            "REST001",
            new List<ItemPedido> { /* ... */ }
        ));

        // Assert
        Assert.True(await harness.Consumed.Any<ValidarPedidoRestaurante>());
        Assert.True(await harness.Published.Any<PedidoRestauranteValidado>());
    }
}
```

### Testar State Machine

```csharp
[Fact]
public async Task DeveConfirmarPedido_QuandoTodasEtapasSucesso()
{
    // Arrange
    var saga = new PedidoSaga();
    var machine = new SagaStateMachineTestHarness<PedidoSaga, EstadoPedido>(saga);

    // Act
    await machine.Publish(new IniciarPedido(/* ... */));
    await machine.Publish(new PedidoRestauranteValidado(/* Valido = true */));
    await machine.Publish(new PagamentoProcessado(/* Sucesso = true */));
    await machine.Publish(new EntregadorAlocado(/* Alocado = true */));
    await machine.Publish(new NotificacaoEnviada(/* ... */));

    // Assert
    Assert.True(await machine.Created.Any());
    var instance = machine.Created.First();
    Assert.Equal(saga.PedidoConfirmado.Name, instance.EstadoAtual);
}
```

---

## 🚨 Troubleshooting

### Problema: Mensagens não são consumidas

**Causas possíveis**:
1. Fila não foi criada (verificar Azure Portal)
2. Connection string inválida
3. Consumer não registrado
4. Endpoint name incorreto

**Solução**:
```bash
# Verificar filas no Azure:
az servicebus queue list --namespace-name sb-saga-poc --resource-group rg-saga-poc

# Verificar logs:
[MassTransit] Receive endpoint started: sb://namespace/fila-restaurante
```

### Problema: SAGA não recebe resposta

**Causas**:
- `CorrelationId` diferente entre comando e resposta
- Consumer não usa `context.RespondAsync`

**Solução**:
```csharp
// Sempre usar o mesmo CorrelationId:
await context.RespondAsync(new PedidoRestauranteValidado(
    CorrelacaoId: context.Message.CorrelacaoId, // ← Usar o mesmo!
    // ...
));
```

### Problema: Dead Letter Queue com muitas mensagens

**Causas**:
- Exceções não tratadas no Consumer
- Timeout muito curto
- Retry policy mal configurada

**Solução**:
```csharp
// Ver mensagens na DLQ:
az servicebus queue show --namespace-name sb-saga-poc --name fila-restaurante/$DeadLetterQueue

// Reprocessar mensagens da DLQ (manualmente):
// No Azure Portal: Service Bus Explorer → Dead-letter → Resubmit
```

---

## 📚 Boas Práticas

### ✅ DO (Faça)

1. **Sempre use CorrelationId**
   ```csharp
   public record MeuComando(Guid CorrelacaoId, ...);
   ```

2. **Torne os Consumers idempotentes**
   ```csharp
   if (await _repo.JaProcessadoAsync(messageId)) return;
   ```

3. **Use Result Pattern (sem exceções)**
   ```csharp
   var resultado = await _servico.ProcessarAsync(...);
   if (resultado.EhFalha) { /* ... */ }
   ```

4. **Log estruturado com CorrelationId**
   ```csharp
   _logger.LogInformation("Pedido validado. CorrelationId: {CorrelationId}", correlationId);
   ```

5. **Configure timeouts adequados**
   ```csharp
   e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
   ```

### ❌ DON'T (Não Faça)

1. ❌ Não use exceções para controle de fluxo
2. ❌ Não compartilhe estado entre Consumers (use SAGA)
3. ❌ Não faça chamadas síncronas HTTP dentro de Consumers (use mensageria)
4. ❌ Não use InMemory em produção
5. ❌ Não ignore erros silenciosamente

---

## 📖 Referências

- **[Documentação Oficial](https://masstransit.io/)** - MassTransit Docs
- **[State Machine](https://masstransit.io/documentation/patterns/saga/state-machine)** - SAGA Pattern
- **[Azure Service Bus](https://masstransit.io/documentation/transports/azure-service-bus)** - Transport
- **[Testing](https://masstransit.io/documentation/concepts/testing)** - Test Harness
- **[CASOS-DE-USO.md](./CASOS-DE-USO.md)** - Exemplos práticos
- **[ARQUITETURA.md](./ARQUITETURA.md)** - Visão geral da arquitetura

---

**Documento criado em**: 2026-01-07
**Versão**: 1.0
**Status**: ✅ Completo
