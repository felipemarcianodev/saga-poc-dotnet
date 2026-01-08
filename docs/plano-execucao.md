# Plano de Execução - POC SAGA Pattern com Rebus e RabbitMQ

## 1. Visão Geral do Projeto

### 1.1 Objetivo
Criar uma Proof of Concept (POC) demonstrando a implementação do **padrão SAGA Orquestrado** utilizando **Rebus** e **RabbitMQ** para comunicação entre microsserviços, aplicando o **Result Pattern** para tratamento de resultados.

### 1.2 Escopo
- **Domínio**: Sistema de Delivery de Comida
- **Padrões**: SAGA Orquestrado + Result Pattern
- **Arquitetura**: Microsserviços com mensageria
- **Mensageria**: Rebus + RabbitMQ (Open Source)
- **Linguagem**: C# (.NET 9.0)
- **Idioma**: Português (código, documentação, tudo)
- **Casos de Uso**: Mínimo 10 cenários com compensações

### 1.3 Estrutura do Projeto
```
saga-poc-dotnet/
├── docs/
│   ├── plano-execucao.md
│   ├── ARQUITETURA.md
│   ├── REBUS-GUIDE.md
│   └── CASOS-DE-USO.md
├── src/
│   ├── SagaPoc.Shared/          # Result Pattern, Mensagens, DTOs
│   ├── SagaPoc.Orquestrador/           # SAGA State Machine (Rebus)
│   ├── SagaPoc.ServicoRestaurante/     # Serviço de Validação de Restaurante
│   ├── SagaPoc.ServicoPagamento/       # Serviço de Processamento de Pagamento
│   ├── SagaPoc.ServicoEntregador/      # Serviço de Alocação de Entregador
│   ├── SagaPoc.ServicoNotificacao/     # Serviço de Notificações
│   └── SagaPoc.Api/                    # API REST (ponto de entrada)
└── SagaPoc.sln
```

---

## 2. Arquitetura da Solução

### 2.1 Fluxo SAGA - Processamento de Pedido de Delivery

```
[API REST]
    ↓ (POST /pedidos)
    ↓
[SAGA Orquestrador] ← RabbitMQ → [Serviços]
    │
    ├──→ 1. [Serviço Restaurante]  → Validar Pedido (aberto, itens disponíveis)
    ├──→ 2. [Serviço Pagamento]    → Processar Pagamento
    ├──→ 3. [Serviço Entregador]   → Alocar Entregador
    └──→ 4. [Serviço Notificação]  → Notificar Cliente

Se FALHA em qualquer etapa → Compensações em ordem reversa
```

### 2.2 Componentes

#### 2.2.1 Rebus
- **State Machine**: Orquestração da SAGA
- **Consumers**: Manipuladores de mensagens em cada serviço
- **Saga Repository**: Persistência do estado da SAGA (In-Memory para POC)

#### 2.2.2 RabbitMQ
- **Filas (Queues)**: Uma fila por serviço
- **Exchanges**: Para roteamento de mensagens
- **Dead Letter Queue**: Mensagens com falha após retries
- **Management UI**: Interface web para monitoramento

#### 2.2.3 Result Pattern
- Encapsulamento de sucesso/falha
- Propagação de erros estruturados
- Sem exceções para controle de fluxo

---

## 3. Fases de Execução

### **FASE 1: Fundação - Result Pattern e Estrutura Base**

#### 3.1.1 Objetivos
- Criar estrutura de pastas e solution
- Implementar Result Pattern em português
- Definir contratos de mensagens

#### 3.1.2 Entregas

##### 1. **Result Pattern (SagaPoc.Shared)**
```csharp
// Tudo em português
public class Resultado<T>
public class Erro
public class Sucesso<T>
public class Falha
```

**Funcionalidades**:
- Conversão implícita
- Fluent API para encadeamento
- Suporte a múltiplos erros
- Serialização JSON
- Métodos auxiliares: `Map()`, `Bind()`, `Match()`

##### 2. **Contratos de Mensagens**
```csharp
// Comandos
public record IniciarPedido(
    Guid CorrelacaoId,
    string ClienteId,
    string RestauranteId,
    List<ItemPedido> Itens,
    string EnderecoEntrega,
    string FormaPagamento
);

public record ValidarPedidoRestaurante(
    Guid CorrelacaoId,
    string RestauranteId,
    List<ItemPedido> Itens
);

public record ProcessarPagamento(
    Guid CorrelacaoId,
    string ClienteId,
    decimal ValorTotal,
    string FormaPagamento
);

public record AlocarEntregador(
    Guid CorrelacaoId,
    string RestauranteId,
    string EnderecoEntrega,
    decimal TaxaEntrega
);

public record NotificarCliente(
    Guid CorrelacaoId,
    string ClienteId,
    string Mensagem,
    TipoNotificacao Tipo
);

// Respostas
public record PedidoRestauranteValidado(
    Guid CorrelacaoId,
    bool Valido,
    decimal ValorTotal,
    int TempoPreparoMinutos,
    string? MotivoRejeicao
);

public record PagamentoProcessado(
    Guid CorrelacaoId,
    bool Sucesso,
    string? TransacaoId,
    string? MotivoFalha
);

public record EntregadorAlocado(
    Guid CorrelacaoId,
    bool Alocado,
    string? EntregadorId,
    int TempoEstimadoMinutos,
    string? MotivoFalha
);

public record NotificacaoEnviada(
    Guid CorrelacaoId,
    bool Enviada
);

// Comandos de Compensação
public record CancelarPedidoRestaurante(Guid CorrelacaoId, string RestauranteId, Guid PedidoId);
public record EstornarPagamento(Guid CorrelacaoId, string TransacaoId);
public record LiberarEntregador(Guid CorrelacaoId, string EntregadorId);
```

##### 3. **Modelos de Domínio**
```csharp
public record ItemPedido(string ProdutoId, string Nome, int Quantidade, decimal PrecoUnitario);

public enum TipoNotificacao
{
    PedidoConfirmado,
    PedidoCancelado,
    EntregadorAlocado,
    PedidoEmPreparacao,
    PedidoSaiuParaEntrega,
    PedidoEntregue
}

public enum StatusPedido
{
    Pendente,
    Confirmado,
    EmPreparacao,
    SaiuParaEntrega,
    Entregue,
    Cancelado
}
```

##### 4. **Estrutura de Projetos**
- `SagaPoc.Shared.csproj` - Class Library
- `SagaPoc.Orquestrador.csproj` - Worker Service
- `SagaPoc.ServicoRestaurante.csproj` - Worker Service
- `SagaPoc.ServicoPagamento.csproj` - Worker Service
- `SagaPoc.ServicoEntregador.csproj` - Worker Service
- `SagaPoc.ServicoNotificacao.csproj` - Worker Service
- `SagaPoc.Api.csproj` - ASP.NET Core Web API

#### 3.1.3 Pacotes NuGet Necessários
```xml
<!-- Todos os projetos -->
<PackageReference Include="Rebus" Version="8.4.4" />
<PackageReference Include="Rebus.RabbitMq" Version="10.1.1" />
<PackageReference Include="Rebus.ServiceProvider" Version="9.0.2" />

<!-- Orquestrador (para Sagas) -->
<PackageReference Include="Rebus.Persistence.InMem" Version="3.0.0" />

<!-- Logging -->
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
```

#### 3.1.4 Critérios de Aceitação
- [ ] Result Pattern permite encadeamento fluente
- [ ] Mensagens fortemente tipadas
- [ ] Solution compila sem warnings
- [ ] Null safety habilitado em todos os projetos

---

### **FASE 2: Configuração Rebus + RabbitMQ**

#### 3.2.1 Objetivos
- Configurar Rebus em todos os serviços
- Configurar RabbitMQ (host, username, password em appsettings)
- Implementar health checks

#### 3.2.2 Entregas

##### 1. **Configuração Base (Cada Serviço)**
```csharp
// Program.cs ou Startup.cs
services.AddRebus(configure => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq(
        $"amqp://{configuration["RabbitMQ:Username"]}:{configuration["RabbitMQ:Password"]}@{configuration["RabbitMQ:Host"]}",
        "fila-restaurante"))
    .Routing(r => r.TypeBased()
        .MapAssemblyOf<ValidarPedidoRestaurante>("fila-restaurante"))
);

// Registrar handlers automaticamente
services.AutoRegisterHandlersFromAssemblyOf<ValidarPedidoRestauranteHandler>();
```

##### 2. **Configuração da API**
```csharp
// Program.cs - API apenas publica mensagens, não consome
services.AddRebus(configure => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMqAsOneWayClient(
        $"amqp://{configuration["RabbitMQ:Username"]}:{configuration["RabbitMQ:Password"]}@{configuration["RabbitMQ:Host"]}"))
    .Routing(r => r.TypeBased()
        .Map<IniciarPedido>("fila-orquestrador"))
);
```

##### 3. **appsettings.json**
```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "saga",
    "Password": "saga123"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

##### 4. **Docker Compose para RabbitMQ**
```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: saga-rabbitmq
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: saga
      RABBITMQ_DEFAULT_PASS: saga123
```

#### 3.2.3 Critérios de Aceitação
- [ ] Todos os serviços conectam ao RabbitMQ
- [ ] Filas criadas automaticamente
- [ ] Health checks retornam status OK
- [ ] Logs estruturados com Serilog
- [ ] RabbitMQ Management UI acessível (http://localhost:15672)

---

### **FASE 3: Implementação da SAGA com Rebus**

> **NOTA**: Rebus não usa State Machines como MassTransit. Em vez disso, usa o padrão Saga Data + Message Handlers.

#### 3.3.1 Objetivos
- Criar Saga Data para armazenar estado do pedido
- Implementar handlers para cada etapa da SAGA
- Implementar fluxo de compensação

#### 3.3.2 Entregas

##### 1. **Saga Data (Estado da SAGA)**
```csharp
public class PedidoSagaData : ISagaData
{
    public Guid Id { get; set; }  // Requerido pelo Rebus
    public int Revision { get; set; }  // Requerido pelo Rebus

    public string EstadoAtual { get; set; } = "Iniciado";

    // Dados do Pedido
    public string ClienteId { get; set; }
    public string RestauranteId { get; set; }
    public decimal ValorTotal { get; set; }
    public string EnderecoEntrega { get; set; }

    // Controle de Compensação
    public string? TransacaoId { get; set; }
    public string? EntregadorId { get; set; }
    public Guid? PedidoRestauranteId { get; set; }

    // Timestamps
    public DateTime DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }

    // Métricas
    public int TempoPreparoMinutos { get; set; }
    public int TempoEntregaMinutos { get; set; }
}
```

##### 2. **Implementação da Saga**
```csharp
public class PedidoSaga : Saga<PedidoSagaData>,
    IAmInitiatedBy<IniciarPedido>,
    IHandleMessages<PedidoRestauranteValidado>,
    IHandleMessages<PagamentoProcessado>,
    IHandleMessages<EntregadorAlocado>,
    IHandleMessages<NotificacaoEnviada>,
    IHandleMessages<RestauranteFalhou>,
    IHandleMessages<PagamentoFalhou>
{
    private readonly IBus _bus;

    public PedidoSaga(IBus bus)
    {
        _bus = bus;
    }

    protected override void CorrelateMessages(ICorrelationConfig<PedidoSagaData> config)
    {
        // Correlacionar mensagens pelo PedidoId
        config.Correlate<IniciarPedido>(m => m.PedidoId, d => d.Id);
        config.Correlate<PedidoRestauranteValidado>(m => m.PedidoId, d => d.Id);
        config.Correlate<PagamentoProcessado>(m => m.PedidoId, d => d.Id);
        config.Correlate<EntregadorAlocado>(m => m.PedidoId, d => d.Id);
        config.Correlate<NotificacaoEnviada>(m => m.PedidoId, d => d.Id);
        config.Correlate<RestauranteFalhou>(m => m.PedidoId, d => d.Id);
        config.Correlate<PagamentoFalhou>(m => m.PedidoId, d => d.Id);
    }

    // Handler inicial - inicia a SAGA
    public async Task Handle(IniciarPedido message)
    {
        if (!IsNew) return; // Evitar duplicação

        Data.ClienteId = message.ClienteId;
        Data.RestauranteId = message.RestauranteId;
        Data.EnderecoEntrega = message.EnderecoEntrega;
        Data.DataInicio = DateTime.UtcNow;
        Data.EstadoAtual = "ValidandoRestaurante";

        await _bus.Send(new ValidarPedidoRestaurante
        {
            PedidoId = Data.Id,
            RestauranteId = message.RestauranteId,
            Itens = message.Itens
        });
    }

    // Handler para restaurante validado
    public async Task Handle(PedidoRestauranteValidado message)
    {
        if (message.Valido)
        {
            Data.ValorTotal = message.ValorTotal;
            Data.TempoPreparoMinutos = message.TempoPreparoMinutos;
            Data.EstadoAtual = "ProcessandoPagamento";

            await _bus.Send(new ProcessarPagamento
            {
                PedidoId = Data.Id,
                ClienteId = Data.ClienteId,
                Valor = Data.ValorTotal
            });
        }
        else
        {
            Data.EstadoAtual = "Cancelado";
            await _bus.Send(new NotificarCliente
            {
                PedidoId = Data.Id,
                Mensagem = $"Pedido cancelado: {message.MotivoRejeicao}"
            });
            MarkAsComplete();
        }
    }

    // Handler para pagamento processado
    public async Task Handle(PagamentoProcessado message)
    {
        Data.TransacaoId = message.TransacaoId;
        Data.EstadoAtual = "AlocandoEntregador";

        await _bus.Send(new AlocarEntregador
        {
            PedidoId = Data.Id,
            EnderecoOrigem = Data.RestauranteId,
            EnderecoDestino = Data.EnderecoEntrega
        });
    }

    // Handler para entregador alocado
    public async Task Handle(EntregadorAlocado message)
    {
        Data.EntregadorId = message.EntregadorId;
        Data.TempoEntregaMinutos = message.TempoEstimadoMinutos;
        Data.EstadoAtual = "NotificandoCliente";

        await _bus.Send(new NotificarCliente
        {
            PedidoId = Data.Id,
            Mensagem = $"Pedido confirmado! Entregador: {message.EntregadorId}"
        });
    }

    // Handler para notificação enviada
    public async Task Handle(NotificacaoEnviada message)
    {
        Data.EstadoAtual = "Concluido";
        Data.DataConclusao = DateTime.UtcNow;
        MarkAsComplete();
    }

    // Handlers de compensação
    public async Task Handle(RestauranteFalhou message)
    {
        Data.EstadoAtual = "Cancelado";
        await _bus.Send(new NotificarCliente
        {
            PedidoId = Data.Id,
            Mensagem = "Pedido cancelado: Erro no restaurante"
        });
        MarkAsComplete();
    }

    public async Task Handle(PagamentoFalhou message)
    {
        // Compensar: Cancelar pedido no restaurante
        await _bus.Send(new CancelarPedidoRestaurante { PedidoId = Data.Id });

        Data.EstadoAtual = "Cancelado";
        await _bus.Send(new NotificarCliente
        {
            PedidoId = Data.Id,
            Mensagem = "Pedido cancelado: Falha no pagamento"
        });
        MarkAsComplete();
    }
}
```

##### 3. **Configuração no Orquestrador**
```csharp
// Program.cs - Orquestrador
services.AddRebus(configure => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq(
        $"amqp://{configuration["RabbitMQ:Username"]}:{configuration["RabbitMQ:Password"]}@{configuration["RabbitMQ:Host"]}",
        "fila-orquestrador"))
    .Sagas(s => s.StoreInMemory()) // Para POC - usar SQL em produção
    .Routing(r => r.TypeBased()
        .MapAssemblyOf<IniciarPedido>("fila-orquestrador")
        .Map<ValidarPedidoRestaurante>("fila-restaurante")
        .Map<ProcessarPagamento>("fila-pagamento")
        .Map<AlocarEntregador>("fila-entregador")
        .Map<NotificarCliente>("fila-notificacao"))
);

// Registrar a Saga automaticamente
services.AutoRegisterHandlersFromAssemblyOf<PedidoSaga>();
```

#### 3.3.3 Critérios de Aceitação
- [ ] State Machine define todos os estados possíveis
- [ ] Compensações executam em ordem reversa
- [ ] Estado persiste entre transições
- [ ] Logs mostram transições de estado

---

### **FASE 4: Implementação dos Serviços (Consumers)**

#### 3.4.1 Objetivos
- Implementar consumers em cada serviço
- Aplicar Result Pattern em toda lógica de negócio
- Simular operações (mock de banco/APIs externas)

#### 3.4.2 Entregas

##### 1. **Serviço de Restaurante**
```csharp
public class ValidarPedidoRestauranteConsumer : IConsumer<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;

    public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
    {
        var resultado = await _servico.ValidarPedidoAsync(
            context.Message.RestauranteId,
            context.Message.Itens
        );

        await context.RespondAsync(new PedidoRestauranteValidado(
            context.Message.CorrelacaoId,
            resultado.EhSucesso,
            resultado.EhSucesso ? resultado.Valor.ValorTotal : 0,
            resultado.EhSucesso ? resultado.Valor.TempoPreparoMinutos : 0,
            resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}

public interface IServicoRestaurante
{
    Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens
    );
}

public record DadosValidacaoPedido(decimal ValorTotal, int TempoPreparoMinutos);

public class ServicoRestaurante : IServicoRestaurante
{
    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        // Simular validação
        if (restauranteId == "REST_FECHADO")
            return Resultado<DadosValidacaoPedido>.Falha("Restaurante fechado no momento");

        if (itens.Any(i => i.ProdutoId == "INDISPONIVEL"))
            return Resultado<DadosValidacaoPedido>.Falha("Um ou mais itens indisponíveis");

        var valorTotal = itens.Sum(i => i.PrecoUnitario * i.Quantidade);
        var tempoPreparo = itens.Count * 10; // 10min por item

        return Resultado<DadosValidacaoPedido>.Sucesso(
            new DadosValidacaoPedido(valorTotal, tempoPreparo)
        );
    }
}
```

##### 2. **Serviço de Pagamento**
```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        var resultado = await _servico.ProcessarAsync(
            context.Message.ClienteId,
            context.Message.ValorTotal,
            context.Message.FormaPagamento
        );

        await context.RespondAsync(new PagamentoProcessado(
            context.Message.CorrelacaoId,
            resultado.EhSucesso,
            resultado.EhSucesso ? resultado.Valor.TransacaoId : null,
            resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}

// Implementar também EstornarPagamentoConsumer para compensação
```

##### 3. **Serviço de Entregador**
```csharp
public class AlocarEntregadorConsumer : IConsumer<AlocarEntregador>
{
    private readonly IServicoEntregador _servico;

    public async Task Consume(ConsumeContext<AlocarEntregador> context)
    {
        var resultado = await _servico.AlocarAsync(
            context.Message.RestauranteId,
            context.Message.EnderecoEntrega,
            context.Message.TaxaEntrega
        );

        await context.RespondAsync(new EntregadorAlocado(
            context.Message.CorrelacaoId,
            resultado.EhSucesso,
            resultado.EhSucesso ? resultado.Valor.EntregadorId : null,
            resultado.EhSucesso ? resultado.Valor.TempoEstimadoMinutos : 0,
            resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}

// Implementar também LiberarEntregadorConsumer para compensação
```

##### 4. **Serviço de Notificação**
```csharp
public class NotificarClienteConsumer : IConsumer<NotificarCliente>
{
    private readonly IServicoNotificacao _servico;

    public async Task Consume(ConsumeContext<NotificarCliente> context)
    {
        var resultado = await _servico.EnviarAsync(
            context.Message.ClienteId,
            context.Message.Mensagem,
            context.Message.Tipo
        );

        await context.RespondAsync(new NotificacaoEnviada(
            context.Message.CorrelacaoId,
            resultado.EhSucesso
        ));
    }
}
```

#### 3.4.3 Critérios de Aceitação
- [ ] Todos os consumers implementados
- [ ] Result Pattern usado em toda lógica de negócio
- [ ] Consumers de compensação implementados
- [ ] Logs estruturados em cada operação

---

### **FASE 5: API REST (Ponto de Entrada)**

#### 3.5.1 Objetivos
- Criar API REST para iniciar SAGA
- Endpoint para consultar status do pedido
- Documentação OpenAPI/Swagger

#### 3.5.2 Entregas

##### 1. **Controller de Pedidos**
```csharp
[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CriarPedido(
        [FromBody] CriarPedidoRequest request)
    {
        var correlacaoId = Guid.NewGuid();

        await _publishEndpoint.Publish(new IniciarPedido(
            correlacaoId,
            request.ClienteId,
            request.RestauranteId,
            request.Itens,
            request.EnderecoEntrega,
            request.FormaPagamento
        ));

        return Accepted(new {
            PedidoId = correlacaoId,
            Mensagem = "Pedido recebido e está sendo processado.",
            Status = "Pendente"
        });
    }

    [HttpGet("{pedidoId}/status")]
    public async Task<IActionResult> ConsultarStatus(Guid pedidoId)
    {
        // Consultar estado da saga
        // Retornar status atual do pedido
        return Ok(new {
            PedidoId = pedidoId,
            Status = "EmProcessamento",
            UltimaAtualizacao = DateTime.UtcNow
        });
    }
}
```

##### 2. **DTOs**
```csharp
public record CriarPedidoRequest(
    string ClienteId,
    string RestauranteId,
    List<ItemPedido> Itens,
    string EnderecoEntrega,
    string FormaPagamento
);
```

#### 3.5.3 Critérios de Aceitação
- [ ] API aceita requisições e retorna 202 Accepted
- [ ] Swagger configurado e funcional
- [ ] Correlação ID retornado para rastreamento

---

### **FASE 6: Casos de Uso e Cenários de Teste**

#### 3.6.1 Casos de Uso Implementados

| # | Caso de Uso | Restaurante | Pagamento | Entregador | Resultado | Compensação |
|---|-------------|-------------|-----------|------------|-----------|-------------|
| 1 | **Pedido Normal** | `REST001` | Aprovado | Disponível | Sucesso | - |
| 2 | **Restaurante Fechado** | `REST_FECHADO` | - | - | ❌ Cancelado | - |
| 3 | **Item Indisponível** | `REST002` | - | - | ❌ Cancelado | - |
| 4 | **Pagamento Recusado** | `REST001` | Recusado | - | ❌ Cancelado | Cancelar no restaurante |
| 5 | **Sem Entregador** | `REST001` | Aprovado | Indisponível | ❌ Cancelado | ⬅️ Estornar pagamento |
| 6 | **Timeout Pagamento** | `REST001` | Timeout | - | ❌ Cancelado | Retry + compensar |
| 7 | **Pedido Premium** | `REST_VIP` | Aprovado | Prioritário | Sucesso | - |
| 8 | **Múltiplos Itens** | `REST001` | Aprovado | Disponível | Sucesso | - |
| 9 | **Endereço Longe** | `REST001` | Aprovado | Motorizado | Taxa alta | - |
| 10 | **Falha Notificação** | `REST001` | Aprovado | Disponível | ⚠️ Pedido OK | - |
| 11 | **Pedido Agendado** | `REST001` | Aprovado | Agendado | Sucesso | - |
| 12 | **Compensação Total** | `REST001` | Aprovado | Falha total | ❌ Rollback | ⬅️ Todas |

#### 3.6.2 Critérios de Aceitação
- [ ] Todos os 12 casos implementados
- [ ] Testes manuais via Swagger/Postman
- [ ] Logs mostram fluxo completo
- [ ] Compensações executadas corretamente

---

### **FASE 7: Documentação Completa**

#### 3.7.1 Objetivos
- README.md detalhado em português
- Documentação de arquitetura
- Guia de configuração do RabbitMQ
- Diagramas de fluxo

#### 3.7.2 Entregas

##### 1. **README.md**
- Visão geral do projeto
- Tecnologias utilizadas
- Como executar localmente
- Configuração do RabbitMQ
- Exemplos de uso
- Casos de uso implementados

##### 2. **ARQUITETURA.md**
- Diagrama de componentes
- Fluxo da SAGA com Rebus
- Explicação do Result Pattern
- Decisões arquiteturais

##### 3. **REBUS-GUIDE.md**
- Como funciona a State Machine
- Configuração do Rebus
- Boas práticas
- Troubleshooting

##### 4. **CASOS-DE-USO.md**
- Detalhamento de cada um dos 12 cenários
- Payloads de exemplo
- Respostas esperadas

#### 3.7.3 Critérios de Aceitação
- [ ] README com instruções claras
- [ ] Diagramas explicativos
- [ ] Comentários XML em todas as APIs públicas
- [ ] Licença MIT incluída

---

## 4. Tecnologias e Ferramentas

### 4.1 Stack Técnico
- **.NET**: 9.0 LTS
- **Linguagem**: C# 13
- **Mensageria**: Rebus 8.1.3
- **Transport**: RabbitMQ
- **Logging**: Serilog
- **API**: ASP.NET Core Web API
- **Documentação**: Swagger/OpenAPI

### 4.2 Serviços Rabbit (Necessários)
- **RabbitMQ**: Namespace Standard ou Premium
- **Filas**:
  - `fila-restaurante`
  - `fila-pagamento`
  - `fila-entregador`
  - `fila-notificacao`
  - `fila-orquestrador-saga`

---

## 5. Configuração do Ambiente

### 5.1 Pré-requisitos
```bash
# .NET 9 SDK
dotnet --version  # >= 9.0

# Git
git --version
```

### 5.2 Configurar appsettings.json
```json
{
  "RabbitMQ": {
    "ConnectionString": "Endpoint=rabbitmq://localhost/;SharedAccessKeyName=..."
  }
}
```

---

## 6. Estrutura Final de Pastas

```
saga-poc-dotnet/
│
├── docs/
│   ├── plano-execucao.md
│   ├── ARQUITETURA.md
│   ├── REBUS-GUIDE.md
│   └── CASOS-DE-USO.md
│
├── src/
│   ├── SagaPoc.Shared/
│   │   ├── ResultPattern/
│   │   │   ├── Resultado.cs
│   │   │   ├── Erro.cs
│   │   │   └── ResultadoExtensions.cs
│   │   ├── Mensagens/
│   │   │   ├── Comandos/
│   │   │   ├── Eventos/
│   │   │   └── Respostas/
│   │   └── Modelos/
│   │
│   ├── SagaPoc.Orquestrador/
│   │   ├── Sagas/
│   │   │   ├── PedidoSaga.cs
│   │   │   └── EstadoPedido.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── SagaPoc.ServicoRestaurante/
│   │   ├── Consumers/
│   │   ├── Servicos/
│   │   └── Program.cs
│   │
│   ├── SagaPoc.ServicoPagamento/
│   │   ├── Consumers/
│   │   └── Servicos/
│   │
│   ├── SagaPoc.ServicoEntregador/
│   │   ├── Consumers/
│   │   └── Servicos/
│   │
│   ├── SagaPoc.ServicoNotificacao/
│   │   ├── Consumers/
│   │   └── Servicos/
│   │
│   └── SagaPoc.Api/
│       ├── Controllers/
│       └── Program.cs
│
├── .gitignore
├── LICENSE
├── README.md
└── SagaPoc.sln
```

---

## 7. Definição de Pronto (DoD)

### 7.1 Código
- Compila sem warnings
- Null safety habilitado
- Tudo em português (classes, variáveis, métodos)
- XML documentation completo
- Serilog configurado

### 7.2 Funcional
- SAGA orquestrada funcionando end-to-end
- 12 casos de uso implementados
- Compensações executando corretamente
- API REST funcional

### 7.3 Documentação
- README.md completo em português
- Arquitetura documentada
- Guia de configuração do RabbitMQ
- Comentários XML em APIs públicas

### 7.4 Repositório
- .gitignore configurado
- Licença MIT
- Pronto para GitHub público

---

## 8. Cronograma Estimado

| Fase | Entregas Principais | Dependências |
|------|---------------------|--------------|
| Fase 1 | Result Pattern + Estrutura | - |
| Fase 2 | Configuração Rebus | Fase 1 |
| Fase 3 | SAGA State Machine | Fase 2 |
| Fase 4 | Implementação dos Serviços | Fase 3 |
| Fase 5 | API REST | Fase 4 |
| Fase 6 | Casos de Uso | Fase 5 |
| Fase 7 | Documentação | Fase 6 |

---

## 9. Próximos Passos (Opcionais - Produção)

> **IMPORTANTE**: Esta POC é material educacional. Os passos abaixo são necessários se você for usar isso em produção de verdade.

### 9.1 Persistência do RabbitMQ

**Problema**: Sem volumes Docker, o RabbitMQ perde todas as filas e mensagens ao executar `docker-compose down` ou remover o container.

**Solução**: Adicionar volumes ao `docker-compose.yml`

```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: saga-rabbitmq
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: saga
      RABBITMQ_DEFAULT_PASS: saga123
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq  # Persistência das mensagens e filas
      - rabbitmq_logs:/var/log/rabbitmq  # Logs (opcional)

volumes:
  rabbitmq_data:
    driver: local
  rabbitmq_logs:
    driver: local
```

**O que resolve**:
- Filas, exchanges e mensagens persistem após `docker-compose down`
- Configurações do RabbitMQ são mantidas
- Logs históricos disponíveis para análise
- Útil para ambientes de desenvolvimento/homologação

**Trade-offs**:
- **Benefício**: Dados sobrevivem a recriação de containers
- **Atenção**: Volumes locais não são replicados (para produção use volumes gerenciados ou clusters)
- **Backup**: Configure backup dos volumes Docker periodicamente

**Estimativa**: 15 minutos (editar docker-compose.yml + testar)

---

### 9.2 Persistência do Estado da SAGA

**Problema**: InMemory repository perde o estado se o orquestrador reiniciar.

**Solução**:
```csharp
// Trocar de:
x.AddRebusSaga<PedidoSaga>()
    .InMemoryRepository();

// Para (SQL Server):
x.AddRebusSaga<PedidoSaga>()
    .EntityFrameworkRepository(r =>
    {
        r.ConcurrencyMode = ConcurrencyMode.Optimistic;
        r.AddDbContext<DbContext, SagaDbContext>((provider, builder) =>
        {
            builder.UseSqlServer(connectionString);
        });
    });

// Ou (Redis):
x.AddRebusSaga<PedidoSaga>()
    .RedisRepository(r =>
    {
        r.DatabaseConfiguration(redisConnectionString);
    });
```

**Trade-offs**:
- SQL Server: Transacional, melhor pra auditoria, mais lento
- Redis: Mais rápido, mas precisa de backup adequado
- MongoDB: Meio termo, boa pra schemas flexíveis

**Estimativa**: 2-4 horas (EF Core setup + migrations)

---

### 9.3 Outbox Pattern (Garantias Transacionais)

**Problema**: Mensagem publicada mas transação no banco falha (ou vice-versa).

**Solução**: Implementar Outbox Pattern com Rebus

```csharp
services.AddDbContext<SagaDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddRebusOutbox();
});

services.AddRebus(x =>
{
    x.AddEntityFrameworkOutbox<SagaDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
});
```

**O que resolve**:
- Garante que mensagens são enviadas APENAS se a transação commitou
- Retry automático de mensagens que falharam
- Histórico de mensagens enviadas (auditoria)

**Estimativa**: 4-8 horas (setup + testes)

**Referência**: [Rebus Outbox](https://github.com/rebus-org/Rebus/documentation/configuration/middleware/outbox)

---

### 9.4 Observabilidade (OpenTelemetry)

**Problema**: Debug de SAGA distribuída é um pesadelo sem tracing.

**Solução**: Adicionar OpenTelemetry + Jaeger (Open Source)

```csharp
// Instalar: dotnet add package OpenTelemetry.Exporter.Jaeger
// Instalar: dotnet add package OpenTelemetry.Instrumentation.AspNetCore
// Instalar: dotnet add package OpenTelemetry.Extensions.Hosting

services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Rebus")
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "localhost";
                options.AgentPort = 6831;
                options.ExportProcessorType = ExportProcessorType.Batch;
            })
            // Alternativa: Console Exporter para desenvolvimento
            .AddConsoleExporter();
    })
    .WithMetrics(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("Rebus")
            .AddPrometheusExporter(); // Para Grafana
    });
```

**Docker Compose para Jaeger**:

```yaml
# Adicionar ao docker-compose.yml
jaeger:
  image: jaegertracing/all-in-one:latest
  ports:
    - "16686:16686"  # UI
    - "6831:6831/udp"  # Agent
  environment:
    - COLLECTOR_OTLP_ENABLED=true
```

**O que rastrear**:
- Duração de cada passo da SAGA
- Taxa de sucesso/falha por tipo de erro
- Compensações executadas
- Dead letter queue (DLQ) metrics
- Distributed tracing entre serviços

**Estimativa**: 4-6 horas (setup + dashboards básicos)

---

### 9.5 Retry Policy e Circuit Breaker

**Problema**: Falhas transitórias (timeout, network blip) matam a SAGA.

**Solução**: Configurar retry policy adequada

```csharp
x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host(connectionString);

    cfg.UseMessageRetry(r =>
    {
        r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5));
        r.Ignore<ValidationException>(); // Não retry erros de validação
    });

    cfg.UseCircuitBreaker(cb =>
    {
        cb.TrackingPeriod = TimeSpan.FromMinutes(1);
        cb.TripThreshold = 15;
        cb.ActiveThreshold = 10;
        cb.ResetInterval = TimeSpan.FromMinutes(5);
    });

    cfg.ConfigureEndpoints(context);
});
```

**Estratégia**:
- Retry: Exponential backoff pra falhas transitórias
- Circuit Breaker: Protege serviços downstream
- Dead Letter Queue: Mensagens que falharam 5+ vezes

**Estimativa**: 2-3 horas

---

### 9.6 Idempotência (Deduplicate Messages)

**Problema**: Retry pode processar a mesma mensagem 2x (duplo débito, etc).

**Solução**: Implementar deduplicação por MessageId

```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IIdempotenciaRepository _idempotencia;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        var messageId = context.MessageId.ToString();

        // Verificar se já processamos
        if (await _idempotencia.JaProcessadoAsync(messageId))
        {
            _logger.LogWarning("Mensagem {MessageId} já processada (duplicada)", messageId);
            return; // Ignorar
        }

        var resultado = await _servico.ProcessarAsync(...);

        // Marcar como processada
        await _idempotencia.MarcarProcessadaAsync(messageId, resultado);

        await context.RespondAsync(...);
    }
}
```

**Storage**:
- Redis (TTL de 7 dias pra MessageIds processados)
- Ou SQL (tabela `MensagensProcessadas`)

**Estimativa**: 3-4 horas

---

### 9.7 Runbooks e Alertas

**Problema**: SAGA travada em produção às 2h da manhã. E agora?

**Solução**: Documentar procedimentos de troubleshooting

#### Runbook: SAGA Travada

```markdown
1. Verificar estado atual da SAGA:
   - Query: SELECT * FROM EstadoPedido WHERE CorrelationId = '{id}'
   - Status esperado vs atual

2. Verificar Dead Letter Queue:
   - Acessar RabbitMQ > fila > Dead Letter
   - Identificar mensagens com erro

3. Ações corretivas:
   - Se timeout: Re-enviar mensagem manualmente
   - Se erro permanente: Executar compensação manual
   - Se duplicata: Limpar estado duplicado

4. Rollback manual (último caso):
   - Script SQL de compensação
   - Verificar integridade após rollback
```

#### Alertas Críticos

- SAGA com > 10 min sem progresso
- Dead Letter Queue com > 100 mensagens
- Taxa de compensação > 10%
- Timeout em > 20% das transações

**Estimativa**: 4-8 horas (documentação + setup de alertas)

---

### 9.8 Testes de Carga e Caos

**Problema**: Funciona com 10 pedidos/min. E com 1000/min?

**Solução**: Testes de carga + Chaos Engineering

```csharp
// NBomber - Teste de Carga
var scenario = ScenarioBuilder
    .CreateScenario("saga-load-test", async context =>
    {
        var response = await httpClient.PostAsJsonAsync("/api/pedidos", pedido);
        return response.IsSuccessStatusCode
            ? Response.Ok()
            : Response.Fail();
    })
    .WithLoadSimulations(
        Simulation.RampingInject(rate: 100, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(5))
    );

NBomberRunner.RegisterScenarios(scenario).Run();
```

**Cenários de Caos**:
1. Matar orquestrador no meio da SAGA (recovery funciona?)
2. RabbitMQ lento (timeout handling OK?)
3. Serviço de Pagamento retornando 500 (compensação executa?)

**Estimativa**: 8-16 horas (setup + execução + análise)

---

### 9.9 Segurança e Autenticação

**Problema**: Mensagens não autenticadas = vulnerabilidade.

**Solução**: Autenticação RabbitMQ + Message Encryption (SSL/TLS)

```csharp
x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host("localhost", 5672, "/", h =>
    {
        h.Username("saga");
        h.Password("saga123");

        // Opcional: Usar SSL/TLS para produção
        h.UseSsl(s =>
        {
            s.Protocol = System.Security.Authentication.SslProtocols.Tls12;
            s.ServerName = "rabbitmq.empresa.com";
            s.CertificatePath = "/path/to/certificate.pfx";
            s.CertificatePassphrase = "certificatePassword";
        });
    });

    // Encryption (opcional) - criptografia em nível de aplicação
    cfg.UseEncryption(new AesEncryptionConfiguration
    {
        Key = encryptionKey
    });
});
```

**Estimativa**: 2-4 horas

---

### 9.10 MongoDB para Auditoria e Histórico da SAGA

**Problema**: Precisamos rastrear o histórico completo de transições da SAGA, armazenar mensagens que falharam na DLQ, e criar snapshots para recuperação rápida.

**Solução**: Usar MongoDB para casos de uso que exigem schema flexível e queries complexas de análise.

#### Casos de Uso

##### 1. **Dead Letter Queue Storage**

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

##### 2. **Histórico de Transições da SAGA**

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

##### 3. **Event Sourcing (Opcional)**

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

##### 5. **Snapshots de Estado da SAGA**

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
    private readonly ISagaRepository<EstadoPedido> _sagaRepository;
    private readonly ISnapshotRepository _snapshotRepository;

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

#### Configuração do MongoDB

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

#### Docker Compose - Adicionar MongoDB

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

#### Queries de Análise Úteis

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

**O que resolve**:
- ✅ Rastreabilidade completa de todas transições da SAGA
- ✅ Análise de mensagens que falharam sem perder contexto
- ✅ Event Sourcing para rebuild de estado
- ✅ Snapshots para recuperação rápida após crash
- ✅ Queries complexas para troubleshooting e análise de negócio

**Trade-offs**:
- **Benefício**: Schema flexível, perfeito para logs e eventos com estrutura variável
- **Atenção**: Não usar para dados transacionais (use PostgreSQL)
- **Performance**: Criar índices adequados para queries frequentes

**Estimativa**: 6-10 horas (setup + implementação dos 4 casos de uso)

---

## 10. Referências

- [Rebus Documentation](https://github.com/rebus-org/Rebus/)
- [Rebus Sagas](https://github.com/rebus-org/Rebus/documentation/patterns/saga)
- [RabbitMQ](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Result Pattern - Vladimir Khorikov](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [SAGA Pattern - Microsoft](https://docs.microsoft.com/azure/architecture/reference-architectures/saga/saga)
- [Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

---

---

### **FASE 9: Integração Completa do Result Pattern no Fluxo de Delivery**

#### 3.9.1 Objetivos
- Refatorar todos os serviços para usar Result Pattern de forma consistente
- Criar classes de resultado específicas para cada domínio
- Implementar validações estruturadas com Result Pattern
- Propagar erros de forma elegante através da SAGA

#### 3.9.2 Entregas

##### 1. **Refatoração dos Serviços de Negócio**

**Serviço de Restaurante - Com Result Pattern Completo**
```csharp
// Resultados específicos do domínio
public record DadosValidacaoPedido(
    decimal ValorTotal,
    int TempoPreparoMinutos,
    List<ItemValidado> ItensValidados
);

public record ItemValidado(
    string ProdutoId,
    string Nome,
    int QuantidadeDisponivel,
    decimal PrecoAtual
);

public class ServicoRestaurante : IServicoRestaurante
{
    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar restaurante
        var validacaoRestaurante = await ValidarRestauranteAsync(restauranteId, cancellationToken);
        if (validacaoRestaurante.EhFalha)
            return Resultado<DadosValidacaoPedido>.Falha(validacaoRestaurante.Erro);

        // 2. Validar itens (encadeamento com Bind)
        return await validacaoRestaurante
            .BindAsync(async _ => await ValidarItensAsync(itens, cancellationToken))
            .BindAsync(async itensValidados => await CalcularValorTotalAsync(itensValidados, cancellationToken));
    }

    private async Task<Resultado<bool>> ValidarRestauranteAsync(
        string restauranteId,
        CancellationToken cancellationToken)
    {
        // Simular consulta ao banco
        await Task.Delay(50, cancellationToken);

        return restauranteId switch
        {
            "REST_FECHADO" => Resultado<bool>.Falha(
                Erro.Negocio("Restaurante está fechado no momento", "RESTAURANTE_FECHADO")
            ),
            "REST_INATIVO" => Resultado<bool>.Falha(
                Erro.Negocio("Restaurante temporariamente indisponível", "RESTAURANTE_INATIVO")
            ),
            _ when restauranteId.StartsWith("REST") => Resultado<bool>.Sucesso(true),
            _ => Resultado<bool>.Falha(
                Erro.NaoEncontrado("Restaurante não encontrado", "RESTAURANTE_NAO_ENCONTRADO")
            )
        };
    }

    private async Task<Resultado<List<ItemValidado>>> ValidarItensAsync(
        List<ItemPedido> itens,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);

        var itensValidados = new List<ItemValidado>();
        var erros = new List<Erro>();

        foreach (var item in itens)
        {
            if (item.ProdutoId == "PROD_INDISPONIVEL")
            {
                erros.Add(Erro.Negocio(
                    $"Produto {item.Nome} está indisponível",
                    "PRODUTO_INDISPONIVEL"
                ));
            }
            else if (item.Quantidade > 10)
            {
                erros.Add(Erro.Validacao(
                    $"Quantidade máxima para {item.Nome} é 10 unidades",
                    "QUANTIDADE_EXCEDIDA"
                ));
            }
            else
            {
                itensValidados.Add(new ItemValidado(
                    item.ProdutoId,
                    item.Nome,
                    QuantidadeDisponivel: 50, // Simulado
                    item.PrecoUnitario
                ));
            }
        }

        return erros.Any()
            ? Resultado<List<ItemValidado>>.Falha(erros)
            : Resultado<List<ItemValidado>>.Sucesso(itensValidados);
    }

    private async Task<Resultado<DadosValidacaoPedido>> CalcularValorTotalAsync(
        List<ItemValidado> itensValidados,
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);

        var valorTotal = itensValidados.Sum(i => i.PrecoAtual * i.QuantidadeDisponivel);
        var tempoPreparo = itensValidados.Count * 15; // 15min por item

        return Resultado<DadosValidacaoPedido>.Sucesso(
            new DadosValidacaoPedido(valorTotal, tempoPreparo, itensValidados)
        );
    }

    // Compensação com Result Pattern
    public async Task<Resultado<Unit>> CancelarPedidoAsync(
        string restauranteId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // Simular cancelamento no sistema do restaurante
        Console.WriteLine($"[Restaurante] Cancelando pedido {pedidoId} no restaurante {restauranteId}");

        return Resultado.Sucesso();
    }
}
```

**Serviço de Pagamento - Com Result Pattern Completo**
```csharp
public record DadosPagamento(
    string TransacaoId,
    DateTime DataProcessamento,
    string Autorizacao
);

public class ServicoPagamento : IServicoPagamento
{
    public async Task<Resultado<DadosPagamento>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar valor
        if (valorTotal <= 0)
            return Resultado<DadosPagamento>.Falha(
                Erro.Validacao("Valor do pagamento deve ser maior que zero", "VALOR_INVALIDO")
            );

        if (valorTotal > 1000)
            return Resultado<DadosPagamento>.Falha(
                Erro.Negocio("Valor excede o limite permitido (R$ 1.000)", "VALOR_EXCEDE_LIMITE")
            );

        // 2. Validar forma de pagamento
        var validacaoForma = ValidarFormaPagamento(formaPagamento);
        if (validacaoForma.EhFalha)
            return Resultado<DadosPagamento>.Falha(validacaoForma.Erro);

        // 3. Processar pagamento (simulado)
        await Task.Delay(200, cancellationToken); // Simular latência de gateway

        // Simular diferentes cenários
        return clienteId switch
        {
            "CLI_SEM_SALDO" => Resultado<DadosPagamento>.Falha(
                Erro.Negocio("Saldo insuficiente", "SALDO_INSUFICIENTE")
            ),
            "CLI_CARTAO_RECUSADO" => Resultado<DadosPagamento>.Falha(
                Erro.Negocio("Cartão recusado pela operadora", "CARTAO_RECUSADO")
            ),
            "CLI_TIMEOUT" => Resultado<DadosPagamento>.Falha(
                Erro.Timeout("Timeout ao processar pagamento", "PAGAMENTO_TIMEOUT")
            ),
            _ => Resultado<DadosPagamento>.Sucesso(new DadosPagamento(
                TransacaoId: $"TXN_{Guid.NewGuid():N}",
                DataProcessamento: DateTime.UtcNow,
                Autorizacao: $"AUTH_{Random.Shared.Next(100000, 999999)}"
            ))
        };
    }

    private Resultado<Unit> ValidarFormaPagamento(string formaPagamento)
    {
        var formasValidas = new[] { "CREDITO", "DEBITO", "PIX", "DINHEIRO" };

        return formasValidas.Contains(formaPagamento.ToUpper())
            ? Resultado.Sucesso()
            : Resultado.Falha(
                Erro.Validacao($"Forma de pagamento '{formaPagamento}' não é válida", "FORMA_PAGAMENTO_INVALIDA")
            );
    }

    // Compensação: Estornar pagamento
    public async Task<Resultado<Unit>> EstornarAsync(
        string transacaoId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (string.IsNullOrEmpty(transacaoId))
            return Resultado.Falha("TransacaoId não pode ser vazio");

        Console.WriteLine($"[Pagamento] Estornando transação {transacaoId}");

        // Simular estorno
        return Resultado.Sucesso();
    }
}
```

**Serviço de Entregador - Com Result Pattern Completo**
```csharp
public record DadosEntregador(
    string EntregadorId,
    string NomeEntregador,
    int TempoEstimadoMinutos,
    string Veiculo
);

public class ServicoEntregador : IServicoEntregador
{
    public async Task<Resultado<DadosEntregador>> AlocarAsync(
        string restauranteId,
        string enderecoEntrega,
        decimal taxaEntrega,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        // Validar taxa
        if (taxaEntrega < 0)
            return Resultado<DadosEntregador>.Falha(
                Erro.Validacao("Taxa de entrega não pode ser negativa", "TAXA_INVALIDA")
            );

        // Simular diferentes cenários
        if (restauranteId == "REST_AREA_REMOTA")
        {
            return Resultado<DadosEntregador>.Falha(
                Erro.Negocio("Nenhum entregador disponível para essa região", "ENTREGADOR_INDISPONIVEL")
            );
        }

        if (enderecoEntrega.Contains("LONGE"))
        {
            return Resultado<DadosEntregador>.Falha(
                Erro.Negocio("Endereço fora da área de cobertura", "AREA_NAO_COBERTA")
            );
        }

        // Alocar entregador
        var entregadorId = $"ENT{Random.Shared.Next(100, 999)}";
        var tempoEstimado = CalcularTempoEstimado(enderecoEntrega);

        return Resultado<DadosEntregador>.Sucesso(new DadosEntregador(
            EntregadorId: entregadorId,
            NomeEntregador: $"Entregador {entregadorId}",
            TempoEstimadoMinutos: tempoEstimado,
            Veiculo: taxaEntrega > 10 ? "Moto" : "Bicicleta"
        ));
    }

    private int CalcularTempoEstimado(string endereco)
    {
        // Simulação simples
        return endereco.Length % 10 + 15; // Entre 15-25 minutos
    }

    // Compensação: Liberar entregador
    public async Task<Resultado<Unit>> LiberarAsync(
        string entregadorId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        Console.WriteLine($"[Entregador] Liberando entregador {entregadorId}");

        return Resultado.Sucesso();
    }
}
```

##### 2. **Atualização dos Consumers para Usar Result Pattern**

```csharp
public class ValidarPedidoRestauranteConsumer : IConsumer<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly ILogger<ValidarPedidoRestauranteConsumer> _logger;

    public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;

        _logger.LogInformation(
            "[Restaurante] Validando pedido {CorrelacaoId} - Restaurante: {RestauranteId}, Itens: {QtdItens}",
            correlacaoId,
            context.Message.RestauranteId,
            context.Message.Itens.Count
        );

        var resultado = await _servico.ValidarPedidoAsync(
            context.Message.RestauranteId,
            context.Message.Itens,
            context.CancellationToken
        );

        // Usar Match para tratar sucesso/falha
        resultado.Match(
            sucesso: dados =>
            {
                _logger.LogInformation(
                    "[Restaurante] Pedido {CorrelacaoId} validado com sucesso - Valor: R$ {Valor:F2}, Tempo: {Tempo}min",
                    correlacaoId,
                    dados.ValorTotal,
                    dados.TempoPreparoMinutos
                );
            },
            falha: erro =>
            {
                _logger.LogWarning(
                    "[Restaurante] Pedido {CorrelacaoId} rejeitado - Motivo: {Motivo} ({Codigo})",
                    correlacaoId,
                    erro.Mensagem,
                    erro.Codigo
                );
            }
        );

        await context.RespondAsync(new PedidoRestauranteValidado(
            correlacaoId,
            Valido: resultado.EhSucesso,
            ValorTotal: resultado.EhSucesso ? resultado.Valor.ValorTotal : 0,
            TempoPreparoMinutos: resultado.EhSucesso ? resultado.Valor.TempoPreparoMinutos : 0,
            MotivoRejeicao: resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}
```

##### 3. **Extensão do Result Pattern com Tipos de Erro**

```csharp
// Adicionar em Erro.cs
public enum TipoErro
{
    Validacao,
    Negocio,
    NaoEncontrado,
    Timeout,
    Infraestrutura,
    Externo
}

public partial class Erro
{
    public TipoErro Tipo { get; }
    public string Codigo { get; }
    public Dictionary<string, object>? Metadata { get; }

    // Factory methods específicos
    public static Erro Validacao(string mensagem, string codigo = "VALIDACAO")
        => new(mensagem, TipoErro.Validacao, codigo);

    public static Erro Negocio(string mensagem, string codigo = "NEGOCIO")
        => new(mensagem, TipoErro.Negocio, codigo);

    public static Erro NaoEncontrado(string mensagem, string codigo = "NAO_ENCONTRADO")
        => new(mensagem, TipoErro.NaoEncontrado, codigo);

    public static Erro Timeout(string mensagem, string codigo = "TIMEOUT")
        => new(mensagem, TipoErro.Timeout, codigo);

    public static Erro Infraestrutura(string mensagem, string codigo = "INFRAESTRUTURA")
        => new(mensagem, TipoErro.Infraestrutura, codigo);

    public static Erro Externo(string mensagem, string codigo = "EXTERNO")
        => new(mensagem, TipoErro.Externo, codigo);
}
```

#### 3.9.3 Critérios de Aceitação
- [ ] Todos os serviços usam Result Pattern consistentemente
- [ ] Erros categorizados por tipo (Validação, Negócio, Timeout, etc)
- [ ] Logs estruturados com contexto de Result
- [ ] Consumers tratam Result Pattern adequadamente
- [ ] Compensações retornam Result Pattern

---

### **FASE 10: Tratamentos de Resiliência**

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

### **FASE 11: Compensação e Rollback Completo**

#### 3.11.1 Objetivos
- Implementar compensação completa em cascata
- Adicionar idempotência nas operações de compensação
- Criar estado de rastreamento de compensações
- Implementar rollback transacional
- Garantir que compensações sejam executadas em ordem reversa

#### 3.11.2 Entregas

##### 1. **Estado Estendido da SAGA com Controle de Compensação**

```csharp
public class EstadoPedido : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string EstadoAtual { get; set; } = string.Empty;

    // ... (propriedades existentes)

    // ==================== Controle de Compensação ====================

    /// <summary>
    /// Indica se o pedido está em processo de compensação.
    /// </summary>
    public bool EmCompensacao { get; set; }

    /// <summary>
    /// Timestamp de início da compensação.
    /// </summary>
    public DateTime? DataInicioCompensacao { get; set; }

    /// <summary>
    /// Timestamp de conclusão da compensação.
    /// </summary>
    public DateTime? DataConclusaoCompensacao { get; set; }

    /// <summary>
    /// Lista de passos compensados com sucesso (para rastreamento).
    /// </summary>
    public List<string> PassosCompensados { get; set; } = new();

    /// <summary>
    /// Indica se a validação do restaurante foi executada (precisa compensar).
    /// </summary>
    public bool RestauranteValidado { get; set; }

    /// <summary>
    /// Indica se o pagamento foi processado (precisa estornar).
    /// </summary>
    public bool PagamentoProcessado { get; set; }

    /// <summary>
    /// Indica se o entregador foi alocado (precisa liberar).
    /// </summary>
    public bool EntregadorAlocado { get; set; }

    /// <summary>
    /// Contador de tentativas de compensação (para idempotência).
    /// </summary>
    public int TentativasCompensacao { get; set; }

    /// <summary>
    /// Erros ocorridos durante a compensação.
    /// </summary>
    public List<string> ErrosCompensacao { get; set; } = new();
}
```

##### 2. **SAGA Estendida com Compensação Completa**

```csharp
public class PedidoSaga : Saga<PedidoSagaData>
{
    // ... (estados e eventos existentes)

    // Novos eventos de compensação
    public Event<PedidoRestauranteCancelado> PedidoCancelado { get; private set; } = null!;
    public Event<PagamentoEstornado> PagamentoEstornado { get; private set; } = null!;
    public Event<EntregadorLiberado> EntregadorLiberado { get; private set; } = null!;

    public PedidoSaga()
    {
        

        // ... (fluxo normal existente)

        // ==================== COMPENSAÇÃO: FALHA NO PAGAMENTO ====================

        During(ProcessandoPagamento,
            When(PagamentoProcessado)
                .IfElse(
                    context => context.Message.Sucesso,
                    // Sucesso
                    aprovado => aprovado
                        .Then(context =>
                        {
                            context.Saga.TransacaoId = context.Message.TransacaoId;
                            context.Saga.PagamentoProcessado = true; // Marcar para compensação
                        })
                        .TransitionTo(AlocandoEntregador)
                        .Publish(context => new AlocarEntregador(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.EnderecoEntrega,
                            context.Saga.TaxaEntrega
                        )),
                    // Falha - Compensar Restaurante
                    recusado => recusado
                        .Then(context =>
                        {
                            context.Saga.MensagemErro = context.Message.MotivoFalha;
                            context.Saga.EmCompensacao = true;
                            context.Saga.DataInicioCompensacao = DateTime.UtcNow;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - COMPENSAÇÃO INICIADA");
                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Motivo: {context.Message.MotivoFalha}");
                        })
                        .TransitionTo(ExecutandoCompensacao)
                        .If(context => context.Saga.RestauranteValidado, // Só compensa se validou
                            compensa => compensa
                                .Publish(context => new CancelarPedidoRestaurante(
                                    context.Saga.CorrelationId,
                                    context.Saga.RestauranteId,
                                    context.Saga.CorrelationId
                                ))
                        )
                )
        );

        // ==================== COMPENSAÇÃO: FALHA NO ENTREGADOR ====================

        During(AlocandoEntregador,
            When(EntregadorAlocado)
                .IfElse(
                    context => context.Message.Alocado,
                    // Sucesso
                    alocado => alocado
                        .Then(context =>
                        {
                            context.Saga.EntregadorId = context.Message.EntregadorId;
                            context.Saga.TempoEntregaMinutos = context.Message.TempoEstimadoMinutos;
                            context.Saga.EntregadorAlocado = true;
                        })
                        .TransitionTo(NotificandoCliente)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido confirmado! Entregador {context.Message.EntregadorId} a caminho.",
                            TipoNotificacao.PedidoConfirmado
                        )),
                    // Falha - Compensar TUDO (Pagamento + Restaurante)
                    semEntregador => semEntregador
                        .Then(context =>
                        {
                            context.Saga.MensagemErro = context.Message.MotivoFalha;
                            context.Saga.EmCompensacao = true;
                            context.Saga.DataInicioCompensacao = DateTime.UtcNow;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - COMPENSAÇÃO TOTAL INICIADA");
                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Compensando: Pagamento + Restaurante");
                        })
                        .TransitionTo(ExecutandoCompensacao)
                        // Compensação em ORDEM REVERSA
                        // 1. Estornar pagamento
                        .If(context => context.Saga.PagamentoProcessado,
                            estorna => estorna
                                .Publish(context => new EstornarPagamento(
                                    context.Saga.CorrelationId,
                                    context.Saga.TransacaoId!
                                ))
                        )
                        // 2. Cancelar no restaurante
                        .If(context => context.Saga.RestauranteValidado,
                            cancela => cancela
                                .Publish(context => new CancelarPedidoRestaurante(
                                    context.Saga.CorrelationId,
                                    context.Saga.RestauranteId,
                                    context.Saga.CorrelationId
                                ))
                        )
                )
        );

        // ==================== TRATAMENTO DE EVENTOS DE COMPENSAÇÃO ====================

        During(ExecutandoCompensacao,
            When(PagamentoEstornado)
                .Then(context =>
                {
                    context.Saga.PassosCompensados.Add($"PagamentoEstornado:{DateTime.UtcNow}");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Pagamento estornado com sucesso");
                })
                .ThenAsync(async context =>
                {
                    // Verificar se todas as compensações foram executadas
                    await FinalizarCompensacaoSeCompleta(context);
                }),

            When(PedidoCancelado)
                .Then(context =>
                {
                    context.Saga.PassosCompensados.Add($"RestauranteCancelado:{DateTime.UtcNow}");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Pedido cancelado no restaurante");
                })
                .ThenAsync(async context =>
                {
                    await FinalizarCompensacaoSeCompleta(context);
                }),

            When(EntregadorLiberado)
                .Then(context =>
                {
                    context.Saga.PassosCompensados.Add($"EntregadorLiberado:{DateTime.UtcNow}");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Entregador liberado");
                })
                .ThenAsync(async context =>
                {
                    await FinalizarCompensacaoSeCompleta(context);
                })
        );
    }

    private async Task FinalizarCompensacaoSeCompleta<T>(BehaviorContext<EstadoPedido, T> context)
        where T : class
    {
        // Verificar se todas as compensações necessárias foram executadas
        var todasCompensadas = true;

        if (context.Saga.PagamentoProcessado &&
            !context.Saga.PassosCompensados.Any(p => p.StartsWith("PagamentoEstornado")))
        {
            todasCompensadas = false;
        }

        if (context.Saga.RestauranteValidado &&
            !context.Saga.PassosCompensados.Any(p => p.StartsWith("RestauranteCancelado")))
        {
            todasCompensadas = false;
        }

        if (context.Saga.EntregadorAlocado &&
            !context.Saga.PassosCompensados.Any(p => p.StartsWith("EntregadorLiberado")))
        {
            todasCompensadas = false;
        }

        if (todasCompensadas)
        {
            context.Saga.DataConclusaoCompensacao = DateTime.UtcNow;
            context.Saga.DataConclusao = DateTime.UtcNow;

            var duracao = (context.Saga.DataConclusaoCompensacao.Value -
                          context.Saga.DataInicioCompensacao!.Value).TotalSeconds;

            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - COMPENSAÇÃO CONCLUÍDA ({duracao:F2}s)");
            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Passos compensados: {context.Saga.PassosCompensados.Count}");

            // Notificar cliente
            await context.Publish(new NotificarCliente(
                context.Saga.CorrelationId,
                context.Saga.ClienteId,
                $"Pedido cancelado: {context.Saga.MensagemErro}. Todos os valores foram estornados.",
                TipoNotificacao.PedidoCancelado
            ));

            // Finalizar SAGA
            context.SetCompleted();
        }
    }
}
```

##### 3. **Consumers de Compensação com Idempotência**

```csharp
public class EstornarPagamentoConsumer : IConsumer<EstornarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<EstornarPagamentoConsumer> _logger;

    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;
        var transacaoId = context.Message.TransacaoId;
        var chaveIdempotencia = $"estorno:{transacaoId}";

        _logger.LogInformation(
            "[Pagamento] Iniciando estorno - CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
            correlacaoId,
            transacaoId
        );

        // ==================== IDEMPOTÊNCIA ====================
        // Verificar se já foi estornado
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[Pagamento] Estorno já processado anteriormente - TransacaoId: {TransacaoId}",
                transacaoId
            );

            // Responder com sucesso mesmo assim (idempotência)
            await context.Publish(new PagamentoEstornado(
                correlacaoId,
                Sucesso: true,
                TransacaoId: transacaoId
            ));
            return;
        }

        // ==================== PROCESSAR ESTORNO ====================
        var resultado = await _servico.EstornarAsync(transacaoId, context.CancellationToken);

        resultado.Match(
            sucesso: _ =>
            {
                _logger.LogInformation(
                    "[Pagamento] Estorno realizado com sucesso - TransacaoId: {TransacaoId}",
                    transacaoId
                );
            },
            falha: erro =>
            {
                _logger.LogError(
                    "[Pagamento] Falha ao estornar - TransacaoId: {TransacaoId}, Erro: {Erro}",
                    transacaoId,
                    erro.Mensagem
                );
            }
        );

        // Marcar como processado
        if (resultado.EhSucesso)
        {
            await _idempotencia.MarcarProcessadoAsync(
                chaveIdempotencia,
                new { transacaoId, data = DateTime.UtcNow }
            );
        }

        await context.Publish(new PagamentoEstornado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            TransacaoId: transacaoId
        ));
    }
}

public class CancelarPedidoRestauranteConsumer : IConsumer<CancelarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<CancelarPedidoRestauranteConsumer> _logger;

    public async Task Consume(ConsumeContext<CancelarPedidoRestaurante> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;
        var pedidoId = context.Message.PedidoId;
        var chaveIdempotencia = $"cancelamento:{pedidoId}";

        _logger.LogInformation(
            "[Restaurante] Cancelando pedido - CorrelacaoId: {CorrelacaoId}, PedidoId: {PedidoId}",
            correlacaoId,
            pedidoId
        );

        // Idempotência
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[Restaurante] Cancelamento já processado - PedidoId: {PedidoId}",
                pedidoId
            );

            await context.Publish(new PedidoRestauranteCancelado(
                correlacaoId,
                Sucesso: true,
                PedidoId: pedidoId
            ));
            return;
        }

        var resultado = await _servico.CancelarPedidoAsync(
            context.Message.RestauranteId,
            pedidoId,
            context.CancellationToken
        );

        if (resultado.EhSucesso)
        {
            await _idempotencia.MarcarProcessadoAsync(chaveIdempotencia, new { pedidoId });
        }

        await context.Publish(new PedidoRestauranteCancelado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            PedidoId: pedidoId
        ));
    }
}

public class LiberarEntregadorConsumer : IConsumer<LiberarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<LiberarEntregadorConsumer> _logger;

    public async Task Consume(ConsumeContext<LiberarEntregador> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;
        var entregadorId = context.Message.EntregadorId;
        var chaveIdempotencia = $"liberacao:{entregadorId}:{correlacaoId}";

        _logger.LogInformation(
            "[Entregador] Liberando entregador - CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
            correlacaoId,
            entregadorId
        );

        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[Entregador] Liberação já processada - EntregadorId: {EntregadorId}",
                entregadorId
            );

            await context.Publish(new EntregadorLiberado(
                correlacaoId,
                Sucesso: true,
                EntregadorId: entregadorId
            ));
            return;
        }

        var resultado = await _servico.LiberarAsync(entregadorId, context.CancellationToken);

        if (resultado.EhSucesso)
        {
            await _idempotencia.MarcarProcessadoAsync(chaveIdempotencia, new { entregadorId });
        }

        await context.Publish(new EntregadorLiberado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            EntregadorId: entregadorId
        ));
    }
}
```

##### 4. **Repositório de Idempotência (InMemory para POC)**

```csharp
public interface IRepositorioIdempotencia
{
    Task<bool> JaProcessadoAsync(string chave);
    Task MarcarProcessadoAsync(string chave, object dados);
}

public class RepositorioIdempotenciaInMemory : IRepositorioIdempotencia
{
    private readonly ConcurrentDictionary<string, (DateTime DataProcessamento, object Dados)> _cache = new();
    private readonly TimeSpan _tempoExpiracao = TimeSpan.FromHours(24);

    public Task<bool> JaProcessadoAsync(string chave)
    {
        if (_cache.TryGetValue(chave, out var entrada))
        {
            // Verificar se não expirou
            if (DateTime.UtcNow - entrada.DataProcessamento < _tempoExpiracao)
            {
                return Task.FromResult(true);
            }

            // Remover entrada expirada
            _cache.TryRemove(chave, out _);
        }

        return Task.FromResult(false);
    }

    public Task MarcarProcessadoAsync(string chave, object dados)
    {
        _cache[chave] = (DateTime.UtcNow, dados);
        return Task.CompletedTask;
    }
}

// Registrar no DI
builder.Services.AddSingleton<IRepositorioIdempotencia, RepositorioIdempotenciaInMemory>();
```

##### 5. **Novos Contratos de Mensagens de Compensação**

```csharp
// Respostas de compensação
public record PagamentoEstornado(
    Guid CorrelacaoId,
    bool Sucesso,
    string TransacaoId
);

public record PedidoRestauranteCancelado(
    Guid CorrelacaoId,
    bool Sucesso,
    Guid PedidoId
);

public record EntregadorLiberado(
    Guid CorrelacaoId,
    bool Sucesso,
    string EntregadorId
);
```

#### 3.11.3 Critérios de Aceitação
- [ ] Compensações executam em ordem reversa corretamente
- [ ] Idempotência garante que compensações não sejam duplicadas
- [ ] Estado da SAGA rastreia passos compensados
- [ ] Logs mostram claramente o fluxo de compensação
- [ ] Compensação parcial funciona (apenas o que foi executado)
- [ ] Compensação total funciona (todos os passos)

---

### **FASE 12: Observabilidade com OpenTelemetry (Jaeger + Prometheus + Grafana)**

#### 3.12.1 Objetivos
- Implementar distributed tracing com Jaeger via OpenTelemetry
- Coletar métricas com Prometheus
- Visualizar métricas e traces no Grafana
- Rastrear duração e taxa de sucesso das SAGAs end-to-end
- Correlacionar logs, métricas e traces

#### 3.12.2 Entregas

##### 1. **Pacotes NuGet Necessários**

```bash
# Shared (criar projeto SagaPoc.Observability)
dotnet new classlib -n SagaPoc.Observability
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Jaeger
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.EntityFrameworkCore
```

##### 2. **Extension Method para OpenTelemetry**

```csharp
// SagaPoc.Observability/OpenTelemetryExtensions.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace SagaPoc.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddSagaOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = environment,
                ["service.namespace"] = "SagaPoc",
                ["host.name"] = Environment.MachineName
            });

        // Verificar se Jaeger está habilitado
        var jaegerEnabled = configuration.GetValue("Jaeger:Enabled", true);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddTelemetrySdk())
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    // Instrumentação automática do ASP.NET Core
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.client_ip", request.HttpContext.Connection.RemoteIpAddress?.ToString());
                            activity.SetTag("http.user_agent", request.Headers["User-Agent"].ToString());
                            activity.SetTag("http.request_content_length", request.ContentLength);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response_content_length", response.ContentLength);
                        };
                    })
                    // Instrumentação de HttpClient
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.request.method", request.Method.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.response.status_code", (int)response.StatusCode);
                        };
                    })
                    // Instrumentação do Entity Framework Core (se usar PostgreSQL para SAGA state)
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.SetDbStatementForText = true;
                        options.SetDbStatementForStoredProcedure = true;
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.query_type", command.CommandType.ToString());
                        };
                    })
                    // Sources customizados (para criar spans manuais)
                    .AddSource("SagaPoc.*")
                    .AddSource("Rebus") // Para rastrear mensagens RabbitMQ
                    .AddConsoleExporter(); // Console exporter sempre habilitado

                // Adicionar Jaeger Exporter (HTTP Binary Thrift)
                if (jaegerEnabled)
                {
                    tracing.AddJaegerExporter(options =>
                    {
                        var jaegerEndpoint = configuration["Jaeger:Endpoint"] ?? "http://localhost:14268/api/traces";
                        options.AgentHost = configuration["Jaeger:AgentHost"] ?? "localhost";
                        options.AgentPort = int.Parse(configuration["Jaeger:AgentPort"] ?? "6831");
                        options.MaxPayloadSizeInBytes = 4096;
                        options.ExportProcessorType = ExportProcessorType.Batch;
                        options.Endpoint = new Uri(jaegerEndpoint);
                        options.Protocol = JaegerExportProtocol.HttpBinaryThrift;
                        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
                        {
                            MaxQueueSize = 2048,
                            ScheduledDelayMilliseconds = 5000,
                            ExporterTimeoutMilliseconds = 30000,
                            MaxExportBatchSize = 512,
                        };
                    });
                }
            })
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter("SagaPoc.*")
                .AddMeter("Rebus")
                .AddPrometheusExporter());

        return services;
    }

    public static WebApplication UseSagaOpenTelemetry(this WebApplication app)
    {
        // Endpoint de métricas Prometheus (/metrics)
        app.MapPrometheusScrapingEndpoint();

        return app;
    }
}
```

##### 3. **Configuração nos Serviços**

```csharp
// Program.cs - Orquestrador
using SagaPoc.Observability;

var builder = WebApplication.CreateBuilder(args);

// Adicionar OpenTelemetry
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.Orquestrador",
    serviceVersion: "1.0.0"
);

// ... resto da configuração

var app = builder.Build();

// Habilitar endpoint de métricas
app.UseSagaOpenTelemetry();

app.Run();
```

```csharp
// Program.cs - API
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.Api",
    serviceVersion: "1.0.0"
);
```

```csharp
// Program.cs - Serviços (Restaurante, Pagamento, Entregador, Notificação)
builder.Services.AddSagaOpenTelemetry(
    builder.Configuration,
    serviceName: "SagaPoc.ServicoRestaurante", // ou ServiçoPagamento, etc.
    serviceVersion: "1.0.0"
);
```

##### 4. **appsettings.json - Configuração**

```json
{
  "Jaeger": {
    "Enabled": true,
    "AgentHost": "localhost",
    "AgentPort": 6831,
    "Endpoint": "http://localhost:14268/api/traces"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Rebus": "Information"
    }
  }
}
```

##### 5. **Docker Compose - Observability Stack**

```yaml
# docker-compose.yml
version: '3.8'

networks:
  saga-network:
    driver: bridge

services:
  # ===================================
  # OBSERVABILITY - TRACING (Jaeger)
  # ===================================
  jaeger:
    image: jaegertracing/all-in-one:latest
    container_name: saga-jaeger
    environment:
      - COLLECTOR_ZIPKIN_HOST_PORT=:9411
      - COLLECTOR_OTLP_ENABLED=true
    networks:
      - saga-network
    ports:
      - "5775:5775/udp"   # Jaeger Agent (Thrift Compact)
      - "6831:6831/udp"   # Jaeger Agent (Thrift Binary)
      - "6832:6832/udp"   # Jaeger Agent (Thrift HTTP)
      - "5778:5778"       # Jaeger Agent Config
      - "16686:16686"     # Jaeger UI
      - "14250:14250"     # Jaeger Collector gRPC
      - "14268:14268"     # Jaeger Collector HTTP (Thrift)
      - "14269:14269"     # Jaeger Collector Health
      - "9411:9411"       # Zipkin Compatible
    restart: unless-stopped

  # ===================================
  # OBSERVABILITY - METRICS (Prometheus)
  # ===================================
  prometheus:
    image: prom/prometheus:latest
    container_name: saga-prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'
    volumes:
      - ./infra/prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    networks:
      - saga-network
    ports:
      - "9090:9090"
    restart: unless-stopped

  # ===================================
  # OBSERVABILITY - DASHBOARDS (Grafana)
  # ===================================
  grafana:
    image: grafana/grafana:latest
    container_name: saga-grafana
    environment:
      - GF_SECURITY_ADMIN_USER=admin
      - GF_SECURITY_ADMIN_PASSWORD=admin123
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana-data:/var/lib/grafana
      - ./infra/grafana/datasources:/etc/grafana/provisioning/datasources:ro
      - ./infra/grafana/dashboards.yml:/etc/grafana/provisioning/dashboards/dashboards.yml:ro
    networks:
      - saga-network
    ports:
      - "3000:3000"
    depends_on:
      - prometheus
      - jaeger
    restart: unless-stopped

  # ===================================
  # OBSERVABILITY - NODE EXPORTER (Opcional)
  # ===================================
  node-exporter:
    image: prom/node-exporter:latest
    container_name: saga-node-exporter
    command:
      - '--path.procfs=/host/proc'
      - '--path.sysfs=/host/sys'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/rootfs:ro
    networks:
      - saga-network
    ports:
      - "9100:9100"
    restart: unless-stopped

volumes:
  prometheus-data:
    driver: local
  grafana-data:
    driver: local
```

##### 6. **Prometheus Configuration**

```yaml
# infra/prometheus/prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  # Prometheus self-monitoring
  - job_name: 'prometheus'
    static_configs:
      - targets: ['localhost:9090']

  # Node Exporter (system metrics)
  - job_name: 'node-exporter'
    static_configs:
      - targets: ['node-exporter:9100']

  # .NET APIs
  - job_name: 'saga-api'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5000']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-api'

  - job_name: 'saga-orquestrador'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5001']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-orquestrador'

  - job_name: 'saga-servico-restaurante'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5002']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-restaurante'

  - job_name: 'saga-servico-pagamento'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5003']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-pagamento'

  - job_name: 'saga-servico-entregador'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5004']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-entregador'

  - job_name: 'saga-servico-notificacao'
    metrics_path: '/metrics'
    static_configs:
      - targets: ['host.docker.internal:5005']
    relabel_configs:
      - source_labels: [__address__]
        target_label: instance
        replacement: 'saga-servico-notificacao'

  # RabbitMQ (se habilitar prometheus plugin)
  - job_name: 'rabbitmq'
    static_configs:
      - targets: ['rabbitmq:15692']
```

##### 7. **Grafana Datasources Configuration**

```yaml
# infra/grafana/datasources/datasources.yml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: false

  - name: Jaeger
    type: jaeger
    access: proxy
    url: http://jaeger:16686
    editable: false
```

```yaml
# infra/grafana/dashboards.yml
apiVersion: 1

providers:
  - name: 'Saga POC Dashboards'
    orgId: 1
    folder: ''
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /var/lib/grafana/dashboards
```

##### 8. **Instrumentação Manual (Opcional)**

```csharp
// Para criar spans customizados
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
            // Lógica de validação...
            var resultado = await ValidarAsync(restauranteId, itens);

            activity?.SetTag("resultado.sucesso", resultado.EhSucesso);

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

#### 3.12.3 URLs de Acesso

Após executar `docker-compose up`:

- **Jaeger UI**: http://localhost:16686
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3000 (admin/admin123)

#### 3.12.4 Queries Úteis no Prometheus

```promql
# Taxa de requisições por segundo
rate(http_server_requests_total[5m])

# Duração P95 das requisições
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# Taxa de erro
rate(http_server_requests_total{status=~"5.."}[5m])
```

#### 3.12.5 Critérios de Aceitação
- [ ] Jaeger exibe traces end-to-end das SAGAs
- [ ] Prometheus coleta métricas de todos os serviços
- [ ] Grafana conectado ao Prometheus e Jaeger
- [ ] Spans incluem tags customizadas (RestauranteId, CorrelacaoId, etc.)
- [ ] Traces mostram propagação através do RabbitMQ
- [ ] Métricas de duração e taxa de erro funcionando

**Estimativa**: 4-6 horas (setup completo + testes)

---

### **FASE 13: Testes de Cenários Complexos**

#### 3.13.1 Objetivos
- Criar testes automatizados para cenários de falha
- Testar compensação em cascata
- Testar resiliência (retry, circuit breaker, timeout)
- Validar idempotência das compensações

#### 3.13.2 Entregas

##### 1. **Testes de Integração com Rebus Test Harness**

```csharp
// Instalar: dotnet add package Rebus.TestFramework

public class PedidoSagaTests
{
    [Fact]
    public async Task DeveCompensarQuandoPagamentoFalhar()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddRebusTestHarness(x =>
            {
                x.AddRebusSaga<PedidoSaga>()
                    .InMemoryRepository();

                x.AddConsumer<ValidarPedidoRestauranteConsumer>();
                x.AddConsumer<ProcessarPagamentoConsumer>();
                x.AddConsumer<CancelarPedidoRestauranteConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Act
        var correlacaoId = NewId.NextGuid();
        await harness.Bus.Publish(new IniciarPedido(
            correlacaoId,
            ClienteId: "CLI_CARTAO_RECUSADO", // Simula falha
            RestauranteId: "REST001",
            Itens: new List<ItemPedido> { new("PROD001", "Pizza", 1, 45.90m) },
            EnderecoEntrega: "Rua Teste, 123",
            FormaPagamento: "CREDITO"
        ));

        // Assert
        var saga = harness.Saga<EstadoPedido, PedidoSaga>();
        Assert.True(await saga.Consumed.Any<IniciarPedido>());

        var instance = saga.Created.ContainsInState(correlacaoId, saga.StateMachine.ExecutandoCompensacao);
        Assert.NotNull(instance);
        Assert.True(instance.EmCompensacao);
        Assert.Contains("RestauranteCancelado", instance.PassosCompensados);
    }

    [Fact]
    public async Task DeveCompensarTudoQuandoEntregadorFalhar()
    {
        // Similar ao teste acima, mas testando falha no entregador
    }

    [Fact]
    public async Task DeveSerIdempotente_QuandoCompensarDuasVezes()
    {
        // Testar que compensação executada 2x não causa duplicidade
    }
}
```

##### 2. **Testes de Carga com NBomber**

```csharp
// Instalar: dotnet add package NBomber

public class TesteCargaSaga
{
    [Fact]
    public void DeveSuportarCargaAlta()
    {
        var scenario = Scenario.Create("saga-load-test", async context =>
        {
            var httpClient = new HttpClient();
            var pedido = new
            {
                clienteId = "CLI001",
                restauranteId = "REST001",
                itens = new[]
                {
                    new { produtoId = "PROD001", nome = "Pizza", quantidade = 1, precoUnitario = 45.90 }
                },
                enderecoEntrega = "Rua Teste, 123",
                formaPagamento = "CREDITO"
            };

            var response = await httpClient.PostAsJsonAsync(
                "http://localhost:5000/api/pedidos",
                pedido
            );

            return response.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.Inject(
                rate: 100,  // 100 requisições por segundo
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromMinutes(5)
            )
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        Assert.True(stats.AllRequestCount > 0);
        Assert.True(stats.FailCount < stats.AllRequestCount * 0.01); // Menos de 1% de falha
    }
}
```

##### 3. **Testes de Chaos Engineering**

```csharp
public class TesteChaos
{
    [Fact]
    public async Task DeveContinuarQuandoOrquestradorReiniciar()
    {
        // 1. Iniciar SAGA
        // 2. Parar orquestrador no meio
        // 3. Reiniciar orquestrador
        // 4. Verificar se SAGA recupera estado e continua
    }

    [Fact]
    public async Task DeveUsarCircuitBreakerQuandoServicoFalhar()
    {
        // Simular falha repetida em serviço e verificar se circuit breaker abre
    }
}
```

#### 3.13.3 Critérios de Aceitação
- [ ] Testes de integração cobrem cenários principais
- [ ] Testes de compensação validam ordem reversa
- [ ] Testes de carga validam throughput
- [ ] Testes de caos validam recuperação

---

### **FASE 14: Documentação e Refinamento Final**

#### 3.14.1 Objetivos
- Criar diagramas de fluxo detalhados
- Documentar runbooks de troubleshooting
- Criar guia de boas práticas
- Adicionar exemplos de uso avançados

#### 3.14.2 Entregas

##### 1. **Diagrama de Compensação em Cascata (Mermaid)**

```markdown
# docs/diagramas-compensacao.md

### Fluxo de Compensação - Falha no Pagamento

```mermaid
sequenceDiagram
    participant API
    participant SAGA
    participant Restaurante
    participant Pagamento

    API->>SAGA: IniciarPedido
    SAGA->>Restaurante: ValidarPedido
    Restaurante-->>SAGA: Validado ✅

    SAGA->>Pagamento: ProcessarPagamento
    Pagamento-->>SAGA: Falha ❌

    Note over SAGA: Iniciando Compensação

    SAGA->>Restaurante: CancelarPedido
    Restaurante-->>SAGA: Cancelado ✅

    Note over SAGA: Compensação Concluída
```

### Fluxo de Compensação - Falha no Entregador (Total)

```mermaid
sequenceDiagram
    participant SAGA
    participant Restaurante
    participant Pagamento
    participant Entregador

    SAGA->>Restaurante: ValidarPedido
    Restaurante-->>SAGA: Validado ✅

    SAGA->>Pagamento: ProcessarPagamento
    Pagamento-->>SAGA: Aprovado ✅

    SAGA->>Entregador: AlocarEntregador
    Entregador-->>SAGA: Indisponível ❌

    Note over SAGA: Compensação Total

    par Compensar em Paralelo
        SAGA->>Pagamento: EstornarPagamento
        SAGA->>Restaurante: CancelarPedido
    end

    Pagamento-->>SAGA: Estornado ✅
    Restaurante-->>SAGA: Cancelado ✅
```
\`\`\`

##### 2. **Runbook de Troubleshooting**

```markdown
# docs/runbook-troubleshooting.md

## Runbook: SAGA Travada

### Sintomas
- SAGA não progride há mais de 10 minutos
- Estado da SAGA não muda
- Mensagens presas na fila

### Diagnóstico

#### 1. Verificar Estado da SAGA
```csharp
// Query no repositório (se SQL)
SELECT * FROM EstadoPedido
WHERE CorrelationId = '{id}'
ORDER BY DataInicio DESC;
```

### Ações Corretivas

#### Caso 1: Timeout em Serviço
- Verificar health check do serviço: `GET /health`
- Reiniciar serviço se necessário
- Re-enviar mensagem manualmente

#### Caso 2: Erro de Validação
- Verificar logs: `grep "{correlacaoId}" logs/*.log`
- Corrigir dados se necessário
- Executar compensação manual

#### Caso 3: Circuit Breaker Aberto
- Aguardar reset interval (5 minutos)
- Verificar saúde do serviço downstream
- Reiniciar se necessário

### Scripts de Compensação Manual

```csharp
// Executar compensação manual via API
POST /api/pedidos/{id}/compensar
{
  "motivo": "Timeout após 10 minutos",
  "executarEstorno": true
}
```
```

##### 3. **Guia de Boas Práticas**

```markdown
# docs/boas-praticas.md

## Boas Práticas - SAGA Pattern

### 1. Idempotência
- ✅ **SEMPRE** verificar idempotência antes de processar
- ✅ Usar chaves únicas (MessageId, TransacaoId, etc)
- ✅ Armazenar em cache/banco com TTL de 24-48h

### 2. Compensações
- ✅ Compensar em **ordem reversa**
- ✅ Compensações devem ser **idempotentes**
- ✅ Não lançar exceções em compensações
- ✅ Logar todas as compensações executadas

### 3. Timeouts
- ✅ Definir timeout para TODAS as operações
- ✅ Usar CancellationToken
- ✅ Timeout maior no orquestrador que nos serviços

### 4. Logs
- ✅ Incluir CorrelationId em TODOS os logs
- ✅ Usar log estruturado (Serilog, NLog)
- ✅ Log de início/fim de cada passo

### 5. Métricas
- ✅ Taxa de sucesso/falha por tipo de erro
- ✅ Duração de cada passo da SAGA
- ✅ Contagem de compensações
- ✅ Taxa de retry

### 6. Testes
- ✅ Testar TODOS os cenários de falha
- ✅ Testar compensação parcial e total
- ✅ Testar idempotência
- ✅ Testes de carga
```

#### 3.14.3 Critérios de Aceitação
- [ ] Diagramas de compensação documentados
- [ ] Runbook de troubleshooting completo
- [ ] Guia de boas práticas criado
- [ ] README.md atualizado com novas funcionalidades

---

### **FASE 15: Migração para RabbitMQ (Open Source)**

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
using SagaPoc.Shared.Mensagens.Comandos;

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
| **Fase 15** | Stack Open Source | RabbitMQ, Docker, MongoDB, Postgres, Grafana | ⭐⭐⭐⭐ |

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
**Última atualização**: 2026-01-07 - Migração para RabbitMQ + Docker + MongoDB + Postgres + Grafana
