using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.Shared.Modelos;

namespace SagaPoc.Orquestrador.Sagas;

/// <summary>
/// Máquina de estados que orquestra o processo completo de um pedido de delivery.
/// Implementa o padrão SAGA orquestrado usando MassTransit.
/// </summary>
public class PedidoSaga : MassTransitStateMachine<EstadoPedido>
{
    // ==================== Estados ====================

    /// <summary>
    /// Estado: Validando pedido com o restaurante.
    /// </summary>
    public State ValidandoRestaurante { get; private set; } = null!;

    /// <summary>
    /// Estado: Processando pagamento.
    /// </summary>
    public State ProcessandoPagamento { get; private set; } = null!;

    /// <summary>
    /// Estado: Alocando entregador.
    /// </summary>
    public State AlocandoEntregador { get; private set; } = null!;

    /// <summary>
    /// Estado: Notificando cliente.
    /// </summary>
    public State NotificandoCliente { get; private set; } = null!;

    /// <summary>
    /// Estado: Pedido confirmado com sucesso.
    /// </summary>
    public State PedidoConfirmado { get; private set; } = null!;

    /// <summary>
    /// Estado: Pedido cancelado (com ou sem compensação).
    /// </summary>
    public State PedidoCancelado { get; private set; } = null!;

    /// <summary>
    /// Estado: Executando compensações.
    /// </summary>
    public State ExecutandoCompensacao { get; private set; } = null!;

    // ==================== Eventos ====================

    /// <summary>
    /// Evento: Iniciar processamento de um novo pedido.
    /// </summary>
    public Event<IniciarPedido> IniciarPedido { get; private set; } = null!;

    /// <summary>
    /// Evento: Pedido validado pelo restaurante.
    /// </summary>
    public Event<PedidoRestauranteValidado> PedidoValidado { get; private set; } = null!;

    /// <summary>
    /// Evento: Pagamento processado.
    /// </summary>
    public Event<PagamentoProcessado> PagamentoProcessado { get; private set; } = null!;

    /// <summary>
    /// Evento: Entregador alocado.
    /// </summary>
    public Event<EntregadorAlocado> EntregadorAlocado { get; private set; } = null!;

    /// <summary>
    /// Evento: Notificação enviada ao cliente.
    /// </summary>
    public Event<NotificacaoEnviada> NotificacaoEnviada { get; private set; } = null!;

    /// <summary>
    /// Construtor da SAGA. Define a máquina de estados e suas transições.
    /// </summary>
    public PedidoSaga()
    {
        // Configurar qual propriedade armazena o estado atual
        InstanceState(x => x.EstadoAtual);

        // ==================== ESTADO INICIAL ====================

        Initially(
            When(IniciarPedido)
                .Then(context =>
                {
                    // Armazenar dados do pedido no estado da SAGA
                    context.Saga.ClienteId = context.Message.ClienteId;
                    context.Saga.RestauranteId = context.Message.RestauranteId;
                    context.Saga.EnderecoEntrega = context.Message.EnderecoEntrega;
                    context.Saga.FormaPagamento = context.Message.FormaPagamento;
                    context.Saga.DataInicio = DateTime.UtcNow;

                    Console.WriteLine($"[SAGA] Iniciando pedido {context.Saga.CorrelationId} - Cliente: {context.Message.ClienteId}");
                })
                .TransitionTo(ValidandoRestaurante)
                .Publish(context => new ValidarPedidoRestaurante(
                    context.Saga.CorrelationId,
                    context.Message.RestauranteId,
                    context.Message.Itens
                ))
        );

        // ==================== VALIDAÇÃO DO RESTAURANTE ====================

        During(ValidandoRestaurante,
            When(PedidoValidado)
                .Then(context =>
                {
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Validação restaurante: {(context.Message.Valido ? "APROVADO" : "REJEITADO")}");
                })
                .IfElse(
                    context => context.Message.Valido,
                    // FLUXO DE SUCESSO: Restaurante validou o pedido
                    aprovado => aprovado
                        .Then(context =>
                        {
                            context.Saga.ValorTotal = context.Message.ValorTotal;
                            context.Saga.TempoPreparoMinutos = context.Message.TempoPreparoMinutos;
                            context.Saga.TaxaEntrega = context.Saga.ValorTotal * 0.15m; // 15% de taxa
                        })
                        .TransitionTo(ProcessandoPagamento)
                        .Publish(context => new ProcessarPagamento(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            context.Saga.ValorTotal + context.Saga.TaxaEntrega, // Total + taxa
                            context.Saga.FormaPagamento
                        )),
                    // FLUXO DE FALHA: Restaurante rejeitou o pedido
                    rejeitado => rejeitado
                        .Then(context =>
                        {
                            context.Saga.MotivoRejeicao = context.Message.MotivoRejeicao;
                            context.Saga.DataConclusao = DateTime.UtcNow;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Cancelado: {context.Message.MotivoRejeicao}");
                        })
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

        // ==================== PROCESSAMENTO DO PAGAMENTO ====================

        During(ProcessandoPagamento,
            When(PagamentoProcessado)
                .Then(context =>
                {
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Pagamento: {(context.Message.Sucesso ? "APROVADO" : "REJEITADO")}");
                })
                .IfElse(
                    context => context.Message.Sucesso,
                    // FLUXO DE SUCESSO: Pagamento aprovado
                    aprovado => aprovado
                        .Then(context =>
                        {
                            context.Saga.TransacaoId = context.Message.TransacaoId;
                        })
                        .TransitionTo(AlocandoEntregador)
                        .Publish(context => new AlocarEntregador(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.EnderecoEntrega,
                            context.Saga.TaxaEntrega
                        )),
                    // FLUXO DE FALHA: Pagamento recusado
                    recusado => recusado
                        .Then(context =>
                        {
                            context.Saga.MensagemErro = context.Message.MotivoFalha;
                            context.Saga.DataConclusao = DateTime.UtcNow;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Pagamento falhou: {context.Message.MotivoFalha}");
                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Iniciando compensação...");
                        })
                        .TransitionTo(ExecutandoCompensacao)
                        // Compensar: Cancelar pedido no restaurante
                        .Publish(context => new CancelarPedidoRestaurante(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.CorrelationId // Usando CorrelationId como PedidoId
                        ))
                        .Then(context =>
                        {
                            // Após compensar, notificar cliente e finalizar
                            context.Saga.DataConclusao = DateTime.UtcNow;
                        })
                        .TransitionTo(PedidoCancelado)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido cancelado: {context.Saga.MensagemErro}",
                            TipoNotificacao.PedidoCancelado
                        ))
                        .Finalize()
                )
        );

        // ==================== ALOCAÇÃO DO ENTREGADOR ====================

        During(AlocandoEntregador,
            When(EntregadorAlocado)
                .Then(context =>
                {
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Entregador: {(context.Message.Alocado ? "ALOCADO" : "INDISPONÍVEL")}");
                })
                .IfElse(
                    context => context.Message.Alocado,
                    // FLUXO DE SUCESSO: Entregador alocado
                    alocado => alocado
                        .Then(context =>
                        {
                            context.Saga.EntregadorId = context.Message.EntregadorId;
                            context.Saga.TempoEntregaMinutos = context.Message.TempoEstimadoMinutos;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Entregador {context.Message.EntregadorId} alocado com sucesso!");
                        })
                        .TransitionTo(NotificandoCliente)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido confirmado! Entregador {context.Message.EntregadorId} a caminho. Tempo estimado: {context.Saga.TempoPreparoMinutos + context.Saga.TempoEntregaMinutos} minutos.",
                            TipoNotificacao.PedidoConfirmado
                        )),
                    // FLUXO DE FALHA: Sem entregadores disponíveis
                    semEntregador => semEntregador
                        .Then(context =>
                        {
                            context.Saga.MensagemErro = context.Message.MotivoFalha;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Falha ao alocar entregador: {context.Message.MotivoFalha}");
                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Iniciando compensação completa...");
                        })
                        .TransitionTo(ExecutandoCompensacao)
                        // Compensar: Estornar pagamento
                        .Publish(context => new EstornarPagamento(
                            context.Saga.CorrelationId,
                            context.Saga.TransacaoId!
                        ))
                        // Compensar: Cancelar pedido no restaurante
                        .Publish(context => new CancelarPedidoRestaurante(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.CorrelationId
                        ))
                        .Then(context =>
                        {
                            context.Saga.DataConclusao = DateTime.UtcNow;
                        })
                        .TransitionTo(PedidoCancelado)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido cancelado: {context.Saga.MensagemErro}. Pagamento estornado.",
                            TipoNotificacao.PedidoCancelado
                        ))
                        .Finalize()
                )
        );

        // ==================== NOTIFICAÇÃO AO CLIENTE ====================

        During(NotificandoCliente,
            When(NotificacaoEnviada)
                .Then(context =>
                {
                    context.Saga.DataConclusao = DateTime.UtcNow;
                    var tempoTotal = (context.Saga.DataConclusao.Value - context.Saga.DataInicio).TotalSeconds;

                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - CONCLUÍDO COM SUCESSO!");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Tempo total de processamento: {tempoTotal:F2}s");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Tempo estimado de entrega: {context.Saga.TempoPreparoMinutos + context.Saga.TempoEntregaMinutos} minutos");
                })
                .TransitionTo(PedidoConfirmado)
                .Finalize()
        );

        // ==================== CONFIGURAÇÕES ADICIONAIS ====================

        // Configurar o que acontece quando a SAGA é finalizada
        SetCompletedWhenFinalized();
    }
}
