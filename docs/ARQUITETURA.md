# Arquitetura - POC SAGA Pattern

Este documento detalha a arquitetura da POC, decisões técnicas, padrões utilizados e fluxos de comunicação entre os componentes.

---

## Visão Geral da Arquitetura

### Arquitetura de Alto Nível

![Diagrama da arquitetura](./images/diagrama-arquitetura.png)
---

## Componentes Principais

### 1. **SagaPoc.Api** (Camada de Entrada)

**Responsabilidade**: Ponto de entrada HTTP para os clientes.

**Tecnologias**:
- ASP.NET Core 9.0
- Swagger/OpenAPI
- Rebus (IBus como One-Way Client)

**Endpoints**:
```csharp
POST   /api/pedidos              # Criar novo pedido
GET    /api/pedidos/{id}/status  # Consultar status do pedido
GET    /health                   # Health check
```

**Fluxo**:
1. Recebe requisição HTTP (POST /api/pedidos)
2. Valida o payload (DataAnnotations)
3. Gera um `CorrelationId` único (Guid)
4. Envia mensagem `IniciarPedido` via Rebus para o RabbitMQ
5. Retorna **202 Accepted** com o `PedidoId`

**Características**:
- Stateless (não mantém estado do pedido)
- Assíncrono (fire-and-forget)
- Idempotente (aceita múltiplas requisições com mesmo payload)

---

### 2. **SagaPoc.Orquestrador** (Camada de Orquestração)

**Responsabilidade**: Coordenar o fluxo da SAGA e gerenciar o estado.

**Tecnologias**:
- Rebus Sagas
- RabbitMQ
- In-Memory Saga Repository (POC) - **Para produção: Postgres**

**Componentes**:
- `PedidoSaga` - Saga com Message Handlers (lógica de transições)
- `PedidoSagaData` - Estado da SAGA (dados persistidos, implementa ISagaData)

**Estados da SAGA**:

![Diagrama de estados do SAGA](./images/diagrama-estados-saga.png)

**Eventos Tratados**:
- `IniciarPedido` → Inicia a SAGA
- `PedidoRestauranteValidado` → Resultado da validação
- `PagamentoProcessado` → Resultado do pagamento
- `EntregadorAlocado` → Resultado da alocação
- `NotificacaoEnviada` → Confirmação de notificação

**Decisões Arquiteturais**:
1. **Por que Rebus Sagas?**
   - Controle centralizado do fluxo via handlers
   - Implementação explícita e clara
   - Suporte nativo a correlação de mensagens
   - Compensações manuais porém explícitas

2. **Por que InMemory para POC?**
   - Simplicidade (sem setup de banco)
   - Rápido para testes
   - **Não usar em produção** (perde estado ao reiniciar)

---

### 3. **Serviços de Domínio** (Camada de Serviços)

Cada serviço é um **Worker Service** independente que consome mensagens do RabbitMQ.

#### 3.1 **SagaPoc.ServicoRestaurante**

**Responsabilidade**: Validar disponibilidade do restaurante e dos itens.

**Handlers**:
- `ValidarPedidoRestauranteHandler` → Valida o pedido
- `CancelarPedidoRestauranteHandler` → Cancela o pedido (compensação)

**Lógica de Validação**:

![Fluxo de validacao](./images/fluxo-validacao.png)

**Compensação**:
```csharp
CancelarPedidoRestaurante
   ↓
Marca o pedido como cancelado no sistema do restaurante
Libera o estoque reservado (se aplicável)
```

---

#### 3.2 **SagaPoc.ServicoPagamento**

**Responsabilidade**: Processar pagamentos e estornos.

**Handlers**:
- `ProcessarPagamentoHandler` → Processa o pagamento
- `EstornarPagamentoHandler` → Estorna o pagamento (compensação)

**Lógica de Processamento**:
```csharp
// Casos de rejeição:
- ClienteId == "CLI_CARTAO_RECUSADO" → Pagamento recusado
- ClienteId == "CLI_TIMEOUT" → Timeout no gateway
- Valor > 1000.00 → Análise de fraude (simulado)

// Casos de sucesso:
- Gera TransacaoId único
- Retorna: PagamentoProcessado
```

**Compensação (Estorno)**:
```csharp
EstornarPagamento
   ↓
Valida TransacaoId
Processa estorno no gateway de pagamento
Gera comprovante de estorno
Idempotente: Executar 2x não duplica estorno
```

**Idempotência**:
```csharp
// Verificar se já foi estornado:
if (await _repositorio.JaEstornadoAsync(transacaoId))
{
    _logger.LogWarning("Estorno já processado (idempotente)");
    return Resultado.Sucesso();
}
```

---

#### 3.3 **SagaPoc.ServicoEntregador**

**Responsabilidade**: Alocar entregadores disponíveis.

**Handlers**:
- `AlocarEntregadorHandler` → Aloca entregador
- `LiberarEntregadorHandler` → Libera o entregador (compensação)

**Lógica de Alocação**:
```csharp
// Casos de indisponibilidade:
- Endereço contém "LONGE" ou "DISTANTE"
- Todos os entregadores ocupados
- Restaurante fora da área de cobertura

// Casos de sucesso:
- Calcula distância (simulada)
- Aloca entregador disponível
- Calcula tempo estimado (15-30min base + distância)
- Reduz 30% do tempo para pedidos VIP
```

**Compensação (Liberar)**:
```csharp
LiberarEntregador
   ↓
Marca o entregador como disponível novamente
Cancela a rota alocada
```

---

#### 3.4 **SagaPoc.ServicoNotificacao**

**Responsabilidade**: Notificar o cliente sobre o status do pedido.

**Handlers**:
- `NotificarClienteHandler` → Envia notificação

**Tipos de Notificação**:
```csharp
enum TipoNotificacao
{
    PedidoConfirmado,
    PedidoCancelado,
    EntregadorAlocado,
    PedidoEmPreparacao,
    PedidoSaiuParaEntrega,
    PedidoEntregue
}
```

**Canais de Notificação** (simulados):
- Email
- SMS
- Push Notification
- WhatsApp

**Tratamento de Falha**:
```csharp
// Notificação NÃO é crítica:
if (ClienteId == "CLI_SEM_NOTIFICACAO")
{
    _logger.LogWarning("Cliente sem notificações habilitadas");
    return Resultado.Sucesso(); // Não cancela o pedido
}
```
---

### 4. **SagaPoc.Shared** (Camada Compartilhada)

**Responsabilidade**: Contratos, modelos e utilitários compartilhados.

**Estrutura**:

![Diagrama de estrutura](./images//diagrama-estrutura.png)

---

## 🔄 Padrões de Design Implementados

### 1. **SAGA Orquestrado (Orchestrated SAGA)**

**O que é?**
- Padrão para transações distribuídas em microsserviços
- Um orquestrador central coordena o fluxo
- Cada serviço executa sua operação local

**Por que Orquestrado (vs Coreografado)?**

| Orquestrado | Coreografado |
|-------------|--------------|
| Controle centralizado | ❌ Lógica espalhada |
| Fácil debug e rastreamento | ❌ Difícil rastrear fluxo completo |
| Compensações explícitas | ❌ Cada serviço conhece os outros |
| Orquestrador é ponto único | Sem ponto único de falha |

**Trade-off**: Escolhemos orquestrado porque:
- POC educacional (mais fácil de entender)
- Fluxo linear claro
- Melhor observabilidade

---

### 2. **Result Pattern**

**O que é?**
Padrão funcional para encapsular sucesso/falha **sem exceções**.

**Estrutura**:
```csharp
public class Resultado<T>
{
    public bool EhSucesso { get; }
    public bool EhFalha => !EhSucesso;
    public T Valor { get; }
    public Erro Erro { get; }

    public static Resultado<T> Sucesso(T valor) => new(valor, null);
    public static Resultado<T> Falha(string mensagem) => new(default, new Erro(mensagem));
}
```

**Por que Result Pattern?**

**Sem Result Pattern** (exceções):
```csharp
try
{
    var pagamento = await ProcessarPagamento();
    var entregador = await AlocarEntregador();
}
catch (PagamentoException ex)
{
    await EstornarPagamento();
    throw;
}
```

**Com Result Pattern**:
```csharp
var resultadoPagamento = await ProcessarPagamento();
if (resultadoPagamento.EhFalha)
{
    await EstornarPagamento();
    return Resultado.Falha(resultadoPagamento.Erro.Mensagem);
}

var resultadoEntregador = await AlocarEntregador();
// ...
```

**Benefícios**:
- Sem try/catch (código mais limpo)
- Erros explícitos no tipo de retorno
- Composição fluente (`Map`, `Bind`)
- Performance (sem overhead de exceções)

---

### 3. **Request/Response Pattern** (Rebus)

**O que é?**
Comunicação síncrona sobre infraestrutura assíncrona usando Reply.

**Fluxo**:
```
[Orquestrador]
    ↓ Send: ValidarPedidoRestaurante
    ↓ (via RabbitMQ)
[Serviço Restaurante]
    ↓ Processa validação
    ↓ Reply: PedidoRestauranteValidado
[Orquestrador]
    ↓ Recebe resposta e continua SAGA
```

**Configuração (Rebus)**:
```csharp
// No Orquestrador (Saga):
await _bus.Send(new ValidarPedidoRestaurante(...));

// No Serviço (Handler):
await _bus.Reply(new PedidoRestauranteValidado(...));
```

**Por que Request/Response?**
- Saga precisa esperar resposta para decidir próximo passo
- Rebus gerencia automaticamente o roteamento da resposta
- Alternativa seria Publish/Subscribe (assíncrono completo)

---

### 4. **Compensating Transaction Pattern**

**O que é?**
Desfazer operações já executadas quando ocorre falha.

**Exemplo (Caso 5 - Sem Entregador)**:
```
1. Restaurante validou → Pedido criado
2. Pagamento aprovado → Cobrança feita
3. Entregador indisponível → FALHA

Compensações (ordem reversa):
   2. Estornar pagamento
   1. Cancelar pedido no restaurante
```

**Implementação na Saga (Rebus)**:
```csharp
public async Task Handle(EntregadorAlocado mensagem)
{
    if (mensagem.Alocado)
    {
        // Sucesso: continua para notificação
        Data.EntregadorId = mensagem.EntregadorId;
        Data.EstadoAtual = "NotificandoCliente";
        await _bus.Send(new NotificarCliente(...));
    }
    else
    {
        // COMPENSAÇÃO: Estornar pagamento
        IniciarCompensacao();
        await _bus.Send(new EstornarPagamento(
            CorrelacaoId: Data.Id,
            TransacaoId: Data.TransacaoId!
        ));
    }
}
```

**Características das Compensações**:
- **Idempotente**: Rodar 2x não causa efeitos colaterais
- **Best-effort**: Tenta executar, mas pode falhar
- **Logged**: Todas as compensações são logadas

---

## 🔌 Comunicação e Mensageria

### RabbitMQ (Transport Layer)

**Configuração (Rebus)**:
```csharp
builder.Services.AddRebus((configure, provider) => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq(
        $"amqp://{username}:{password}@{host}",
        "nome-da-fila"))
    .Routing(r => r.TypeBased()
        .Map<ValidarPedidoRestaurante>("fila-restaurante")
        .Map<ProcessarPagamento>("fila-pagamento"))
    .Options(o =>
    {
        o.SetNumberOfWorkers(1);
        o.SetMaxParallelism(10);
    })
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<PrimeiroHandler>();
```

**Filas Criadas Automaticamente no RabbitMQ**:
```
RabbitMQ (localhost:5672)
├── fila-restaurante              (Comandos para Serviço Restaurante)
├── fila-pagamento                (Comandos para Serviço Pagamento)
├── fila-entregador               (Comandos para Serviço Entregador)
├── fila-notificacao              (Comandos para Serviço Notificação)
└── fila-orquestrador             (Mensagens para a SAGA)
```

**Dead Letter Queue (DLQ)**:
- Mensagens que falharam após N tentativas vão para DLQ
- RabbitMQ gerencia automaticamente
- Rebus suporta configuração de retry policy e error handling

---

### Contratos de Mensagem (Strongly Typed)

**Comandos** (Requests):
```csharp
public record ValidarPedidoRestaurante(
    Guid CorrelacaoId,
    string RestauranteId,
    List<ItemPedido> Itens
);
```

**Respostas** (Responses):
```csharp
public record PedidoRestauranteValidado(
    Guid CorrelacaoId,
    bool Valido,
    decimal ValorTotal,
    int TempoPreparoMinutos,
    string? MotivoRejeicao
);
```

**Por que Records?**
- Imutáveis por padrão
- Equality by value (útil para testes)
- Sintaxe concisa
- Serialização JSON automática

---

## Persistência e Estado

### Estado da SAGA

**Modelo (Rebus)**:
```csharp
public class PedidoSagaData : ISagaData
{
    // Propriedades obrigatórias do Rebus
    public Guid Id { get; set; }                  // Chave primária (CorrelationId)
    public int Revision { get; set; }             // Controle de concorrência

    public string EstadoAtual { get; set; }       // Estado atual da SAGA

    // Dados do Pedido
    public string ClienteId { get; set; }
    public string RestauranteId { get; set; }
    public decimal ValorTotal { get; set; }
    public string EnderecoEntrega { get; set; }

    // Controle de Compensação
    public string? TransacaoId { get; set; }      // Para estorno
    public string? EntregadorId { get; set; }     // Para liberação
    public bool EmCompensacao { get; set; }       // Flag de compensação
    public List<string> PassosCompensados { get; set; } // Tracking

    // Timestamps
    public DateTime DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }
}
```

**Persistência (POC vs Produção)**:

| Ambiente | Repository | Persistência | Quando usar |
|----------|-----------|--------------|-------------|
| **POC** | InMemory | RAM | Testes, desenvolvimento |
| **Produção** | EntityFramework + SQL | Banco relacional | Auditoria, consistência ACID |
| **Produção** | Redis | Cache distribuído | Alta performance |

**Configuração SQL (Produção com Rebus)**:
```csharp
// No Program.cs do Orquestrador
builder.Services.AddRebus((configure, provider) => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq(...))
    .Sagas(s => s.UseNpgsql(connectionString, "Sagas"))
    // Ou MongoDB:
    // .Sagas(s => s.UseMongoDb(connectionString, "sagas"))
);
```

---

## Confiabilidade e Resiliência

### 1. **Retry Policy**

**Configuração com Rebus (Produção)**:
```csharp
builder.Services.AddRebus((configure, provider) => configure
    .Transport(t => t.UseRabbitMq(...))
    .Options(o =>
    {
        o.SimpleRetryStrategy(maxDeliveryAttempts: 5);
        // Ou estratégia customizada:
        o.RetryStrategy(
            secondLevelRetriesEnabled: true,
            errorDetailsHeaderMaxLength: 500
        );
    })
);
```

**Estratégia**:
- 1ª tentativa: imediata
- 2ª tentativa: após 1s
- 3ª tentativa: após 6s
- 4ª tentativa: após 16s
- 5ª tentativa: após 30s
- Após 5 falhas: Dead Letter Queue

---

### 2. **Idempotência**

**Problema**: Retry pode processar a mesma mensagem 2x.

**Solução**: Verificar se já foi processado.

```csharp
public async Task Handle(EstornarPagamento mensagem)
{
    var chaveIdempotencia = $"estorno:{mensagem.TransacaoId}";

    if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
    {
        _logger.LogWarning("Estorno já processado (duplicado)");
        return; // Ignorar
    }

    // Processar estorno...

    await _idempotencia.MarcarProcessadaAsync(chaveIdempotencia);
    await _bus.Reply(new PagamentoEstornado(...));
}
```

---

### 3. **Circuit Breaker** (Produção)

**Observação**: Rebus não possui Circuit Breaker nativo. Para implementar, use:
- **Polly**: Biblioteca de resiliência .NET
- **Integração**: Aplicar políticas Polly nos handlers

```csharp
// Exemplo com Polly
var circuitBreakerPolicy = Policy
    .Handle<Exception>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 15,
        durationOfBreak: TimeSpan.FromMinutes(5)
    );

// Aplicar no handler
await circuitBreakerPolicy.ExecuteAsync(async () =>
{
    await ProcessarPagamentoAsync();
});
```

---

## Observabilidade

### Stack Completa Implementada (Fase 12)

A POC implementa observabilidade completa usando **OpenTelemetry**, **Jaeger**, **Prometheus** e **Grafana**.

---

### 1. Logging Estruturado (Serilog)

**Configuração**:
```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Servico", "SagaPoc.Orquestrador")
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();
```

**Campos Logados**:
- `CorrelationId` - Rastreamento end-to-end
- `EstadoAtual` - Estado da SAGA
- `Servico` - Qual serviço gerou o log
- `Timestamp` - Data/hora
- `Duracao` - Tempo de execução

**Exemplo de Log**:
```json
{
  "Timestamp": "2026-01-07T16:45:32Z",
  "Level": "Information",
  "Message": "Pedido validado com sucesso",
  "Properties": {
    "CorrelationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "RestauranteId": "REST001",
    "ValorTotal": 45.90,
    "TempoPreparo": 10,
    "Servico": "SagaPoc.ServicoRestaurante"
  }
}
```

---

### 2. Distributed Tracing (OpenTelemetry + Jaeger)

**Configuração OpenTelemetry**:
```csharp
// SagaPoc.Observability/OpenTelemetryExtensions.cs
services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(serviceName, serviceVersion)
        .AddTelemetrySdk())
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("SagaPoc.*")
            .AddSource("Rebus")
            .AddJaegerExporter();
    })
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());
```

**URL Jaeger UI**: http://localhost:16686

**Trace de uma SAGA completa**:
```
[Trace: a1b2c3d4-e5f6-7890-abcd-ef1234567890]
├── [Span] SagaPoc.Api: POST /api/pedidos (75ms)
│   └── Tags: http.method=POST, http.status_code=202
├── [Span] SagaPoc.Orquestrador: ValidarPedidoRestaurante (150ms)
│   ├── Tags: saga.correlation_id, saga.state=ValidandoRestaurante
│   └── [Span] RabbitMQ: Publish (ValidarPedidoRestaurante)
├── [Span] SagaPoc.ServicoRestaurante: ValidarPedidoConsumer (120ms)
│   └── Tags: restaurante.id=REST001, valor_total=45.90
├── [Span] SagaPoc.Orquestrador: ProcessarPagamento (450ms)
│   └── [Span] RabbitMQ: Publish (ProcessarPagamento)
├── [Span] SagaPoc.ServicoPagamento: ProcessarPagamentoConsumer (400ms)
│   └── Tags: transacao.id=TXN_abc123, payment.approved=true
├── [Span] SagaPoc.Orquestrador: AlocarEntregador (220ms)
│   └── [Span] RabbitMQ: Publish (AlocarEntregador)
├── [Span] SagaPoc.ServicoEntregador: AlocarEntregadorConsumer (180ms)
│   └── Tags: entregador.id=ENT001, tempo_estimado=25min
└── [Span] SagaPoc.ServicoNotificacao: NotificarClienteConsumer (80ms)
    └── Tags: notification.type=PedidoConfirmado, notification.sent=true

Total Trace Duration: 1075ms
```

**Propagação de Contexto**:
- Propagação automática através do RabbitMQ via Rebus
- Header `traceparent` incluído em todas as mensagens
- W3C Trace Context padrão
- Rebus suporta activity propagation nativamente

---

### 3. Métricas (Prometheus)

**URL Prometheus**: http://localhost:9090

**Endpoint de Métricas**: http://localhost:5000/metrics (cada serviço expõe)

**Configuração do Prometheus** (`docker/infra/prometheus/prometheus.yml`):
```yaml
scrape_configs:
  - job_name: 'saga-api'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['saga-api:8080']
```

**Métricas Coletadas Automaticamente**:

#### HTTP Metrics:
```promql
# Taxa de requisições por segundo
rate(http_server_requests_total[5m])

# Duração P50, P90, P95, P99
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# Taxa de erro (status 5xx)
rate(http_server_requests_total{status=~"5.."}[5m])

# Requisições por endpoint
sum by (endpoint) (rate(http_server_requests_total[5m]))
```

#### Runtime Metrics:
```promql
# Uso de memória
dotnet_total_memory_bytes

# GC Collections
rate(dotnet_collection_count_total[5m])

# Thread pool
dotnet_threadpool_num_threads
```

#### Custom Metrics (Rebus):
```promql
# Mensagens enviadas/recebidas
rate(rebus_messages_sent_total[5m])
rate(rebus_messages_received_total[5m])

# Falhas de processamento
rate(rebus_messages_failed_total[5m])

# Duração de processamento de mensagens
histogram_quantile(0.95, rate(rebus_message_duration_seconds_bucket[5m]))
```

---

### 4. Dashboards (Grafana)

**URL Grafana**: http://localhost:3000 (admin/admin123)

**Datasources Configurados**:
- Prometheus (http://prometheus:9090)
- Jaeger (http://jaeger:16686)

**Dashboards Sugeridos**:

#### Dashboard 1: Visão Geral da SAGA
- Taxa de SAGAs iniciadas vs concluídas (por minuto)
- Taxa de sucesso vs falha (%)
- Duração P50/P95/P99 das SAGAs
- Taxa de compensações executadas

#### Dashboard 2: Saúde dos Serviços
- Latência por serviço (P95)
- Taxa de erro por serviço
- Throughput (req/s)
- Taxa de disponibilidade (uptime)

#### Dashboard 3: RabbitMQ
- Mensagens na fila por serviço
- Taxa de publicação/consumo
- Mensagens não confirmadas (unacked)
- Dead Letter Queue

#### Dashboard 4: Sistema
- CPU usage por container
- Memória usage por container
- Disk I/O
- Network traffic

**Painel de Exemplo (Grafana Query)**:
```promql
# Taxa de sucesso de SAGAs (últimas 5min)
sum(rate(http_server_requests_total{endpoint="/api/pedidos",status="202"}[5m]))
/
sum(rate(http_server_requests_total{endpoint="/api/pedidos"}[5m]))
* 100
```

---

### 5. Instrumentação Customizada

**Criar Spans Manualmente**:
```csharp
using System.Diagnostics;

public class ServicoRestaurante
{
    private static readonly ActivitySource ActivitySource = new("SagaPoc.ServicoRestaurante");

    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        using var activity = ActivitySource.StartActivity("ValidarPedidoRestaurante");
        activity?.SetTag("restaurante.id", restauranteId);
        activity?.SetTag("itens.count", itens.Count);

        try
        {
            var resultado = await ValidarAsync(restauranteId, itens);

            activity?.SetTag("resultado.sucesso", resultado.EhSucesso);
            activity?.SetTag("valor_total", resultado.Valor?.ValorTotal);

            if (resultado.EhFalha)
            {
                activity?.SetStatus(ActivityStatusCode.Error, resultado.Erro.Mensagem);
            }

            return resultado;
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

**Tags Customizadas Úteis**:
- `saga.correlation_id` - ID da SAGA
- `saga.state` - Estado atual
- `compensacao.executada` - Se compensação foi executada
- `restaurante.id`, `cliente.id`, `entregador.id` - IDs de negócio
- `transaction.id` - ID da transação de pagamento

---

### 6. Configuração nos Serviços

**Todos os serviços .NET estão configurados**:

#### API (SagaPoc.Api/Program.cs):
```csharp
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.Api",
    serviceVersion: "1.0.0"
);

// ...

app.UseSagaOpenTelemetry(); // Expõe endpoint /metrics
```

#### Workers (Orquestrador, Serviços):
```csharp
builder.AddSagaOpenTelemetryForHost(
    serviceName: "SagaPoc.Orquestrador",
    serviceVersion: "1.0.0"
);
```

**Configuração via appsettings.json**:
```json
{
  "Jaeger": {
    "Enabled": true,
    "AgentHost": "jaeger",
    "AgentPort": 6831,
    "Endpoint": "http://jaeger:14268/api/traces"
  }
}
```

---

### 7. Stack Docker Completa

**docker-compose.yml** inclui:
- Jaeger (all-in-one)
- Prometheus
- Grafana (com datasources pré-configurados)
- Node Exporter
- RabbitMQ
- Todos os 6 serviços .NET

**Iniciar toda a stack**:
```bash
cd docker
docker-compose up -d
```

**URLs de Acesso**:
- Jaeger: http://localhost:16686
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin123)
- RabbitMQ: http://localhost:15672 (saga/saga123)
- API: http://localhost:5000

---

## Decisões Arquiteturais

### Por que Rebus (e não outros)?

| Alternativa | Prós | Contras | Quando usar |
|-------------|------|---------|-------------|
| **Rebus** | Leve, simples, flexível, Sagas com handlers explícitos | Sem State Machine visual, menos recursos prontos | POC, projetos médios, controle explícito |
| **MassTransit** | State Machine integrada, Retry/CB embutidos | Curva de aprendizado, mais complexo | SAGA complexas, múltiplos transportes |
| **NServiceBus** | Mais maduro, suporte enterprise | Pago | Enterprise |
| **RabbitMQ direto** | Controle total | Muito boilerplate | Necessita customização extrema |

---

### Por que RabbitMQ (e não Kafka)?

| Transport | Prós | Contras | Quando usar |
|-----------|------|---------|-------------|
| **RabbitMQ** | Dead Letter Queue, garantia de ordem, Request/Reply nativo | Menor vazão que Kafka | SAGA, POC, Request/Response |
| **Kafka** | Alta vazão, log distribuído, retenção longa | Overkill para SAGA, sem Request/Reply | Event Sourcing, analytics, streaming |
| **Azure Service Bus** | Gerenciado, integração Azure | Custo, vendor lock-in | Cloud Azure |

---

## Segurança (Produção)

### 1. **Autenticação RabbitMQ**

Usar usuários específicos por serviço com permissões limitadas:
```csharp
// Configuração Rebus com autenticação
.Transport(t => t.UseRabbitMq(
    $"amqp://{username}:{password}@rabbitmq.production.com",
    "fila-servico"
))
// Ou usar AMQPS (AMQP over TLS):
// amqps://username:password@rabbitmq.production.com
```

### 2. **Encryption at Rest**

Configurar RabbitMQ com persistência criptografada usando plugins.

### 3. **Encryption in Transit**

- HTTPS/TLS para API REST
- AMQPS (AMQP over TLS) para RabbitMQ
- Certificados SSL/TLS válidos

---

## Referências

- **[plano-execucao.md](./plano-execucao/plano-execucao.md.md)** - Plano completo do projeto
- **[guia-rebus.md](./guia-rebus.md)** - Guia do Rebus
- **[casos-uso.md](./casos-uso.md)** - 12 cenários implementados
- **[Rebus Documentation](https://github.com/rebus-org/Rebus)** - Documentação oficial
- **[SAGA Pattern - Microsoft](https://docs.microsoft.com/azure/architecture/reference-architectures/saga/saga)**

---

**Documento criado em**: 2026-01-07
**Versão**: 2.0
**Última atualização**: 2026-01-08 - Atualizado para Rebus (migração do MassTransit concluída)
**Status**: Completo
