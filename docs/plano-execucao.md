# Plano de Execução - POC SAGA Pattern com MassTransit e Azure Service Bus

## 1. Visão Geral do Projeto

### 1.1 Objetivo
Criar uma Proof of Concept (POC) demonstrando a implementação do **padrão SAGA Orquestrado** utilizando **MassTransit** e **Azure Service Bus** para comunicação entre microsserviços, aplicando o **Result Pattern** para tratamento de resultados.

### 1.2 Escopo
- **Domínio**: Sistema de Delivery de Comida
- **Padrões**: SAGA Orquestrado + Result Pattern
- **Arquitetura**: Microsserviços com mensageria
- **Mensageria**: MassTransit + Azure Service Bus
- **Linguagem**: C# (.NET 8.0)
- **Idioma**: Português (código, documentação, tudo)
- **Casos de Uso**: Mínimo 10 cenários com compensações

### 1.3 Estrutura do Projeto
```
saga-poc-dotnet/
├── docs/
│   ├── PLANO-EXECUCAO.md
│   ├── ARQUITETURA.md
│   ├── MASSTRANSIT-GUIDE.md
│   └── CASOS-DE-USO.md
├── src/
│   ├── SagaPoc.Shared/          # Result Pattern, Mensagens, DTOs
│   ├── SagaPoc.Orquestrador/           # SAGA State Machine (MassTransit)
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
[SAGA Orquestrador] ← Azure Service Bus → [Serviços]
    │
    ├──→ 1. [Serviço Restaurante]  → Validar Pedido (aberto, itens disponíveis)
    ├──→ 2. [Serviço Pagamento]    → Processar Pagamento
    ├──→ 3. [Serviço Entregador]   → Alocar Entregador
    └──→ 4. [Serviço Notificação]  → Notificar Cliente

Se FALHA em qualquer etapa → Compensações em ordem reversa
```

### 2.2 Componentes

#### 2.2.1 MassTransit
- **State Machine**: Orquestração da SAGA
- **Consumers**: Manipuladores de mensagens em cada serviço
- **Saga Repository**: Persistência do estado da SAGA (In-Memory para POC)

#### 2.2.2 Azure Service Bus
- **Filas**: Uma fila por serviço
- **Tópicos**: Para eventos de domínio (opcional)
- **Dead Letter Queue**: Mensagens com falha

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
<PackageReference Include="MassTransit" Version="8.1.3" />
<PackageReference Include="MassTransit.Azure.ServiceBus.Core" Version="8.1.3" />

<!-- Orquestrador -->
<PackageReference Include="MassTransit.StateMachine" Version="8.1.3" />

<!-- API -->
<PackageReference Include="MassTransit.AspNetCore" Version="8.1.3" />

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

### **FASE 2: Configuração MassTransit + Azure Service Bus**

#### 3.2.1 Objetivos
- Configurar MassTransit em todos os serviços
- Configurar Azure Service Bus (connection string em appsettings)
- Implementar health checks

#### 3.2.2 Entregas

##### 1. **Configuração Base (Cada Serviço)**
```csharp
services.AddMassTransit(x =>
{
    // Registrar consumers
    x.AddConsumer<ValidarPedidoRestauranteConsumer>();

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(configuration["AzureServiceBus:ConnectionString"]);

        cfg.ReceiveEndpoint("fila-restaurante", e =>
        {
            e.ConfigureConsumer<ValidarPedidoRestauranteConsumer>(context);
        });
    });
});
```

##### 2. **Configuração da API**
```csharp
services.AddMassTransit(x =>
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(configuration["AzureServiceBus:ConnectionString"]);
    });
});

// Request Client para iniciar SAGA
services.AddScoped<IRequestClient<IniciarPedido>>();
```

##### 3. **appsettings.json**
```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://..."
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

#### 3.2.3 Critérios de Aceitação
- [ ] Todos os serviços conectam ao Azure Service Bus
- [ ] Filas criadas automaticamente
- [ ] Health checks retornam status OK
- [ ] Logs estruturados com Serilog

---

### **FASE 3: Implementação da SAGA State Machine**

#### 3.3.1 Objetivos
- Criar State Machine no projeto Orquestrador
- Definir estados e eventos da SAGA
- Implementar fluxo de compensação

#### 3.3.2 Entregas

##### 1. **Estado da SAGA**
```csharp
public class EstadoPedido : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string EstadoAtual { get; set; }

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

##### 2. **State Machine**
```csharp
public class PedidoSaga : MassTransitStateMachine<EstadoPedido>
{
    // Estados
    public State ValidandoRestaurante { get; private set; }
    public State ProcessandoPagamento { get; private set; }
    public State AlocandoEntregador { get; private set; }
    public State NotificandoCliente { get; private set; }
    public State PedidoConfirmado { get; private set; }
    public State PedidoCancelado { get; private set; }

    // Eventos
    public Event<IniciarPedido> IniciarPedido { get; private set; }
    public Event<PedidoRestauranteValidado> PedidoValidado { get; private set; }
    public Event<PagamentoProcessado> PagamentoProcessado { get; private set; }
    public Event<EntregadorAlocado> EntregadorAlocado { get; private set; }
    public Event<NotificacaoEnviada> NotificacaoEnviada { get; private set; }

    public PedidoSaga()
    {
        InstanceState(x => x.EstadoAtual);

        Initially(
            When(IniciarPedido)
                .Then(context => {
                    context.Saga.ClienteId = context.Message.ClienteId;
                    context.Saga.RestauranteId = context.Message.RestauranteId;
                    context.Saga.EnderecoEntrega = context.Message.EnderecoEntrega;
                    context.Saga.DataInicio = DateTime.UtcNow;
                })
                .TransitionTo(ValidandoRestaurante)
                .Publish(context => new ValidarPedidoRestaurante(
                    context.Saga.CorrelationId,
                    context.Message.RestauranteId,
                    context.Message.Itens
                ))
        );

        During(ValidandoRestaurante,
            When(PedidoValidado)
                .IfElse(context => context.Message.Valido,
                    valido => valido
                        .Then(context => {
                            context.Saga.ValorTotal = context.Message.ValorTotal;
                            context.Saga.TempoPreparoMinutos = context.Message.TempoPreparoMinutos;
                        })
                        .TransitionTo(ProcessandoPagamento)
                        .Publish(context => new ProcessarPagamento(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            context.Saga.ValorTotal,
                            context.Data.FormaPagamento
                        )),
                    invalido => invalido
                        .TransitionTo(PedidoCancelado)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido cancelado: {context.Message.MotivoRejeicao}",
                            TipoNotificacao.PedidoCancelado
                        ))
                )
        );

        During(ProcessandoPagamento,
            When(PagamentoProcessado)
                .IfElse(context => context.Message.Sucesso,
                    sucesso => sucesso
                        .Then(context => context.Saga.TransacaoId = context.Message.TransacaoId)
                        .TransitionTo(AlocandoEntregador)
                        .Publish(context => new AlocarEntregador(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.EnderecoEntrega,
                            context.Saga.ValorTotal * 0.15m // 15% de taxa
                        )),
                    falha => falha
                        .TransitionTo(PedidoCancelado)
                        // Compensar restaurante se necessário
                )
        );

        During(AlocandoEntregador,
            When(EntregadorAlocado)
                .IfElse(context => context.Message.Alocado,
                    alocado => alocado
                        .Then(context => {
                            context.Saga.EntregadorId = context.Message.EntregadorId;
                            context.Saga.TempoEntregaMinutos = context.Message.TempoEstimadoMinutos;
                        })
                        .TransitionTo(NotificandoCliente)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido confirmado! Entregador {context.Message.EntregadorId} alocado.",
                            TipoNotificacao.PedidoConfirmado
                        )),
                    semEntregador => semEntregador
                        // Compensar: estornar pagamento
                        .Publish(context => new EstornarPagamento(
                            context.Saga.CorrelationId,
                            context.Saga.TransacaoId!
                        ))
                        .TransitionTo(PedidoCancelado)
                )
        );

        During(NotificandoCliente,
            When(NotificacaoEnviada)
                .Then(context => context.Saga.DataConclusao = DateTime.UtcNow)
                .TransitionTo(PedidoConfirmado)
                .Finalize()
        );
    }
}
```

##### 3. **Configuração no Orquestrador**
```csharp
services.AddMassTransit(x =>
{
    x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
        .InMemoryRepository(); // Para POC - usar Redis/SQL em produção

    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(configuration["AzureServiceBus:ConnectionString"]);
        cfg.ConfigureEndpoints(context);
    });
});
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
- Guia de configuração do Azure Service Bus
- Diagramas de fluxo

#### 3.7.2 Entregas

##### 1. **README.md**
- Visão geral do projeto
- Tecnologias utilizadas
- Como executar localmente
- Configuração do Azure Service Bus
- Exemplos de uso
- Casos de uso implementados

##### 2. **ARQUITETURA.md**
- Diagrama de componentes
- Fluxo da SAGA com MassTransit
- Explicação do Result Pattern
- Decisões arquiteturais

##### 3. **MASSTRANSIT-GUIDE.md**
- Como funciona a State Machine
- Configuração do MassTransit
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
- **.NET**: 8.0 LTS
- **Linguagem**: C# 12
- **Mensageria**: MassTransit 8.1.3
- **Transport**: Azure Service Bus
- **Logging**: Serilog
- **API**: ASP.NET Core Web API
- **Documentação**: Swagger/OpenAPI

### 4.2 Serviços Azure (Necessários)
- **Azure Service Bus**: Namespace Standard ou Premium
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
# .NET 8 SDK
dotnet --version  # >= 8.0

# Azure CLI (para criar Service Bus)
az --version

# Git
git --version
```

### 5.2 Criar Azure Service Bus
```bash
# Login
az login

# Criar Resource Group
az group create --name rg-saga-poc --location brazilsouth

# Criar Service Bus Namespace
az servicebus namespace create \
  --name sb-saga-poc-dotnet \
  --resource-group rg-saga-poc \
  --location brazilsouth \
  --sku Standard

# Obter Connection String
az servicebus namespace authorization-rule keys list \
  --resource-group rg-saga-poc \
  --namespace-name sb-saga-poc-dotnet \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString --output tsv
```

### 5.3 Configurar appsettings.json
```json
{
  "AzureServiceBus": {
    "ConnectionString": "Endpoint=sb://sb-saga-poc-dotnet.servicebus.windows.net/;SharedAccessKeyName=..."
  }
}
```

---

## 6. Estrutura Final de Pastas

```
saga-poc-dotnet/
│
├── docs/
│   ├── PLANO-EXECUCAO.md
│   ├── ARQUITETURA.md
│   ├── MASSTRANSIT-GUIDE.md
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
- Guia de configuração do Azure SB
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
| Fase 2 | Configuração MassTransit | Fase 1 |
| Fase 3 | SAGA State Machine | Fase 2 |
| Fase 4 | Implementação dos Serviços | Fase 3 |
| Fase 5 | API REST | Fase 4 |
| Fase 6 | Casos de Uso | Fase 5 |
| Fase 7 | Documentação | Fase 6 |

---

## 9. Próximos Passos (Opcionais - Produção)

> **IMPORTANTE**: Esta POC é material educacional. Os passos abaixo são necessários se você for usar isso em produção de verdade.

### 9.1 Persistência do Estado da SAGA

**Problema**: InMemory repository perde o estado se o orquestrador reiniciar.

**Solução**:
```csharp
// Trocar de:
x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
    .InMemoryRepository();

// Para (SQL Server):
x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
    .EntityFrameworkRepository(r =>
    {
        r.ConcurrencyMode = ConcurrencyMode.Optimistic;
        r.AddDbContext<DbContext, SagaDbContext>((provider, builder) =>
        {
            builder.UseSqlServer(connectionString);
        });
    });

// Ou (Redis):
x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
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

### 9.2 Outbox Pattern (Garantias Transacionais)

**Problema**: Mensagem publicada mas transação no banco falha (ou vice-versa).

**Solução**: Implementar Outbox Pattern com MassTransit

```csharp
services.AddDbContext<SagaDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.AddMassTransitOutbox();
});

services.AddMassTransit(x =>
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

**Referência**: [MassTransit Outbox](https://masstransit.io/documentation/configuration/middleware/outbox)

---

### 9.3 Observabilidade (OpenTelemetry)

**Problema**: Debug de SAGA distribuída é um pesadelo sem tracing.

**Solução**: Adicionar OpenTelemetry + Application Insights/Jaeger

```csharp
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
    })
    .WithMetrics(builder =>
    {
        builder
            .AddMeter("MassTransit")
            .AddAzureMonitorMetricExporter();
    });
```

**O que rastrear**:
- Duração de cada passo da SAGA
- Taxa de sucesso/falha por tipo de erro
- Compensações executadas
- Dead letter queue (DLQ) metrics

**Estimativa**: 4-6 horas (setup + dashboards básicos)

---

### 9.4 Retry Policy e Circuit Breaker

**Problema**: Falhas transitórias (timeout, network blip) matam a SAGA.

**Solução**: Configurar retry policy adequada

```csharp
x.UsingAzureServiceBus((context, cfg) =>
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

### 9.5 Idempotência (Deduplicate Messages)

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

### 9.6 Runbooks e Alertas

**Problema**: SAGA travada em produção às 2h da manhã. E agora?

**Solução**: Documentar procedimentos de troubleshooting

#### Runbook: SAGA Travada

```markdown
1. Verificar estado atual da SAGA:
   - Query: SELECT * FROM EstadoPedido WHERE CorrelationId = '{id}'
   - Status esperado vs atual

2. Verificar Dead Letter Queue:
   - Acessar Azure Service Bus > fila > Dead Letter
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

### 9.7 Testes de Carga e Caos

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
2. Azure Service Bus lento (timeout handling OK?)
3. Serviço de Pagamento retornando 500 (compensação executa?)

**Estimativa**: 8-16 horas (setup + execução + análise)

---

### 9.8 Segurança e Autenticação

**Problema**: Mensagens não autenticadas = vulnerabilidade.

**Solução**: Managed Identity + Message Encryption

```csharp
x.UsingAzureServiceBus((context, cfg) =>
{
    // Usar Managed Identity (sem connection string)
    cfg.Host(new Uri("sb://sb-saga-poc.servicebus.windows.net"), h =>
    {
        h.ConfigureOptions(options =>
        {
            options.TransportType = ServiceBusTransportType.AmqpWebSockets;
            options.TokenCredential = new DefaultAzureCredential();
        });
    });

    // Encryption (opcional)
    cfg.UseEncryption(new AesEncryptionConfiguration
    {
        Key = encryptionKey
    });
});
```

**Estimativa**: 2-4 horas

---

## 10. Referências

- [MassTransit Documentation](https://masstransit.io/)
- [MassTransit Sagas](https://masstransit.io/documentation/patterns/saga)
- [Azure Service Bus](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Result Pattern - Vladimir Khorikov](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [SAGA Pattern - Microsoft](https://docs.microsoft.com/azure/architecture/reference-architectures/saga/saga)
- [Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

---

**Documento criado em**: 2026-01-06
**Versão**: 3.1 (Contexto: Sistema de Delivery de Comida)
**Idioma**: Português (BR)
**Última atualização**: Adicionados passos opcionais para produção
