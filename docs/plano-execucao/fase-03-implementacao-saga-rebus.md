# FASE 3: Implementação da SAGA com Rebus


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

