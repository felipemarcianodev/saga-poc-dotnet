# FASE 15: Migração para RabbitMQ (Open Source)


#### 3.15.1 Objetivos
- Substituir RabbitMQ por RabbitMQ
- Configurar RabbitMQ localmente (via Docker ou instalação)
- Atualizar todos os serviços para usar RabbitMQ
- Manter todas as políticas de resiliência (Retry, Circuit Breaker, DLQ)
- Tornar projeto 100% open source e executável localmente

#### 3.15.2 Entregas

##### 1. **RabbitMQ via Docker (Simples)**

```yaml
# docker-compose.yml (raiz do projeto)
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: saga-rabbitmq
    hostname: saga-rabbitmq
    ports:
      - "5672:5672"   # AMQP protocol
      - "15672:15672" # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: saga
      RABBITMQ_DEFAULT_PASS: saga123
      RABBITMQ_DEFAULT_VHOST: /
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  rabbitmq_data:
```

**Comandos:**
```bash
# Subir RabbitMQ
docker-compose up -d

# Acessar Management UI
# http://localhost:15672 (saga/saga123)
```

##### 2. **Pacotes NuGet - Adicionar RabbitMQ**

```bash
# Adicionar RabbitMQ em TODOS os projetos
dotnet add src/SagaPoc.Orquestrador package Rebus.RabbitMQ
dotnet add src/SagaPoc.Api package Rebus.RabbitMQ
dotnet add src/SagaPoc.ServicoRestaurante package Rebus.RabbitMQ
dotnet add src/SagaPoc.ServicoPagamento package Rebus.RabbitMQ
dotnet add src/SagaPoc.ServicoEntregador package Rebus.RabbitMQ
dotnet add src/SagaPoc.ServicoNotificacao package Rebus.RabbitMQ

# Adicionar health check do RabbitMQ
dotnet add src/SagaPoc.Orquestrador package AspNetCore.HealthChecks.Rabbitmq
```

##### 3. **Configuração do Orquestrador com RabbitMQ**

```csharp
// src/SagaPoc.Orquestrador/Program.cs
using Rebus;
using SagaPoc.Orquestrador;
using SagaPoc.Orquestrador.Consumers;
using SagaPoc.Orquestrador.Sagas;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Orquestrador SAGA com RabbitMQ");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
        .AddRabbitMQ(
            rabbitConnectionString: $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:5672",
            name: "rabbitmq",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: new[] { "messaging" }
        );

    // ==================== MASSTRANSIT COM RABBITMQ ====================
    builder.Services.AddRebus(x =>
    {
        // Configurar SAGA State Machine
        x.AddRebusSaga<PedidoSaga>()
            .InMemoryRepository(); // Para POC - usar MongoDB/Redis em produção

        // Configurar Dead Letter Queue Consumer
        x.AddConsumer<DeadLetterQueueConsumer>();

        // ==================== RABBITMQ CONFIGURATION ====================
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"]!);
                h.Password(builder.Configuration["RabbitMQ:Password"]!);
            });

            // ============ RETRY POLICY ============
            cfg.UseMessageRetry(retry =>
            {
                retry.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(2)
                );

                // Retry apenas em erros transitórios
                retry.Handle<TimeoutException>();
                retry.Handle<HttpRequestException>();
            });

            // ============ CIRCUIT BREAKER ============
            cfg.UseCircuitBreaker(cb =>
            {
                cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                cb.TripThreshold = 15;
                cb.ActiveThreshold = 10;
                cb.ResetInterval = TimeSpan.FromMinutes(5);
            });

            // ============ PREFETCH COUNT ============
            // Limita quantas mensagens cada worker consome simultaneamente
            cfg.PrefetchCount = 16;

            // ============ DEAD LETTER QUEUE ============
            cfg.ReceiveEndpoint("fila-dead-letter", e =>
            {
                e.ConfigureConsumer<DeadLetterQueueConsumer>(context);
            });

            // Configurar endpoints automaticamente
            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    host.Run();

    Log.Information("Orquestrador SAGA encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Orquestrador SAGA falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
```

##### 4. **Configuração dos Serviços (Restaurante, Pagamento, Entregador, Notificação)**

```csharp
// src/SagaPoc.ServicoRestaurante/Program.cs
using Rebus;
using SagaPoc.ServicoRestaurante.Consumers;
using SagaPoc.ServicoRestaurante.Servicos;

var builder = Host.CreateApplicationBuilder(args);

// Serviços de domínio
builder.Services.AddScoped<IServicoRestaurante, ServicoRestaurante>();

// ==================== MASSTRANSIT COM RABBITMQ ====================
builder.Services.AddRebus(x =>
{
    // Registrar consumers
    x.AddConsumer<ValidarPedidoRestauranteConsumer>();
    x.AddConsumer<CancelarPedidoRestauranteConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });

        // Retry policy
        cfg.UseMessageRetry(retry =>
        {
            retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2));
            retry.Handle<TimeoutException>();
        });

        // ============ FILA ESPECÍFICA DO RESTAURANTE ============
        cfg.ReceiveEndpoint("fila-restaurante", e =>
        {
            e.ConfigureConsumer<ValidarPedidoRestauranteConsumer>(context);
            e.ConfigureConsumer<CancelarPedidoRestauranteConsumer>(context);

            // Configurações de performance
            e.PrefetchCount = 16;
            e.UseConcurrencyLimit(10); // Máximo 10 mensagens processadas simultaneamente
        });
    });
});

var host = builder.Build();
host.Run();
```

**Aplicar o mesmo padrão para todos os outros serviços (Pagamento, Entregador, Notificação).**

##### 5. **Configuração da API**

```csharp
// src/SagaPoc.Api/Program.cs
using Rebus;
using SagaPoc.Common.Mensagens.Comandos;

var builder = WebApplication.CreateBuilder(args);

// ==================== MASSTRANSIT COM RABBITMQ ====================
builder.Services.AddRebus(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]!);
            h.Password(builder.Configuration["RabbitMQ:Password"]!);
        });
    });
});

// Request Client para iniciar SAGA
builder.Services.AddScoped<IRequestClient<IniciarPedido>>();

var app = builder.Build();
app.Run();
```

##### 6. **appsettings.json - Configurações**

```json
// Aplicar em TODOS os projetos (Orquestrador, API, Serviços)
{
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "saga",
    "Password": "saga123"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Rebus": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

##### 7. **Como Rodar o Projeto**

```bash
# 1. Subir RabbitMQ
docker-compose up -d

# 2. Verificar se RabbitMQ está rodando
docker ps
# Acessar: http://localhost:15672 (saga/saga123)

# 3. Rodar os serviços (abrir 5 terminais)

# Terminal 1 - Orquestrador
cd src/SagaPoc.Orquestrador
dotnet run

# Terminal 2 - Serviço Restaurante
cd src/SagaPoc.ServicoRestaurante
dotnet run

# Terminal 3 - Serviço Pagamento
cd src/SagaPoc.ServicoPagamento
dotnet run

# Terminal 4 - Serviço Entregador
cd src/SagaPoc.ServicoEntregador
dotnet run

# Terminal 5 - API
cd src/SagaPoc.Api
dotnet run

# 4. Testar
curl -X POST http://localhost:5000/api/pedidos \
  -H "Content-Type: application/json" \
  -d '{
    "clienteId": "CLI_001",
    "restauranteId": "REST_001",
    "itens": [{"nome": "Pizza", "quantidade": 1, "preco": 45.00}],
    "enderecoEntrega": "Rua A, 123",
    "formaPagamento": "CREDITO"
  }'

# 5. Monitorar mensagens
# Abrir RabbitMQ Management UI: http://localhost:15672/#/queues
# Ver filas: fila-restaurante, fila-pagamento, fila-entregador, fila-dead-letter
```

#### 3.15.3 Critérios de Aceitação
- [ ] Pacotes RabbitMQ removidos de todos os projetos
- [ ] Pacotes RabbitMQ instalados em todos os projetos
- [ ] RabbitMQ rodando via Docker
- [ ] Todos os serviços conectam ao RabbitMQ com sucesso
- [ ] Filas criadas automaticamente pelo Rebus
- [ ] Health check do RabbitMQ funcionando
- [ ] RabbitMQ Management UI acessível (localhost:15672)
- [ ] Políticas de resiliência mantidas (Retry, Circuit Breaker, DLQ)
- [ ] Mensagens fluindo entre serviços
- [ ] Projeto roda 100% localmente sem dependências de cloud

---

## Resumo das Fases 9-15

| Fase | Foco Principal | Entregas Chave | Complexidade |
|------|----------------|----------------|--------------|
| **Fase 9** | Result Pattern no Delivery | Refatoração de serviços, validações estruturadas | ⭐⭐⭐ |
| **Fase 10** | Resiliência | Retry, Circuit Breaker, Timeout, DLQ | ⭐⭐⭐⭐ |
| **Fase 11** | Compensação Completa | Rollback em cascata, idempotência | ⭐⭐⭐⭐⭐ |
| **Fase 12** | Observabilidade | Logs estruturados, métricas, dashboards | ⭐⭐⭐ |
| **Fase 13** | Testes Complexos | Testes de integração, carga, chaos | ⭐⭐⭐⭐ |
| **Fase 14** | Documentação | Diagramas, runbooks, boas práticas | ⭐⭐ |
| **Fase 15** | Stack Open Source | RabbitMQ, Docker, Postgres, Seq | ⭐⭐⭐⭐ |

---

## Cronograma Sugerido (Fases 9-15)

| Fase | Tempo Estimado | Dependências |
|------|----------------|--------------|
| Fase 9  | 6-8 horas | Fases 1-8 concluídas |
| Fase 10 | 8-10 horas | Fase 9 |
| Fase 11 | 10-12 horas | Fase 10 |
| Fase 12 | 4-6 horas | Fase 11 |
| Fase 13 | 8-10 horas | Fase 12 |
| Fase 14 | 4-6 horas | Fase 13 |
| Fase 15 | 6-8 horas | Opcional - pode ser feita em paralelo |
| **TOTAL** | **46-60 horas** | - |

---

**Documento criado em**: 2026-01-06
**Versão**: 5.0 (Adicionada Fase 15 - Stack Open Source Completa)
**Idioma**: Português (BR)
**Última atualização**: 2026-01-07 - Migração para RabbitMQ + Docker + Postgres + Seq
