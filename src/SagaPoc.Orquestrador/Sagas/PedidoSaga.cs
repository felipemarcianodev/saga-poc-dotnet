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
    /// Evento: Pedido cancelado no restaurante (compensação).
    /// </summary>
    public Event<PedidoRestauranteCancelado> PedidoCanceladoRestaurante { get; private set; } = null!;

    /// <summary>
    /// Evento: Pagamento estornado (compensação).
    /// </summary>
    public Event<PagamentoEstornado> PagamentoEstornado { get; private set; } = null!;

    /// <summary>
    /// Evento: Entregador liberado (compensação).
    /// </summary>
    public Event<EntregadorLiberado> EntregadorLiberado { get; private set; } = null!;

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
                            context.Saga.RestauranteValidado = true; // Marcar para compensação
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
                            context.Saga.PagamentoProcessado = true; // Marcar para compensação
                        })
                        .TransitionTo(AlocandoEntregador)
                        .Publish(context => new AlocarEntregador(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.EnderecoEntrega,
                            context.Saga.TaxaEntrega
                        )),
                    // FLUXO DE FALHA: Pagamento recusado - Compensar Restaurante
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
                            context.Saga.EntregadorAlocado = true; // Marcar para compensação

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Entregador {context.Message.EntregadorId} alocado com sucesso!");
                        })
                        .TransitionTo(NotificandoCliente)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido confirmado! Entregador {context.Message.EntregadorId} a caminho. Tempo estimado: {context.Saga.TempoPreparoMinutos + context.Saga.TempoEntregaMinutos} minutos.",
                            TipoNotificacao.PedidoConfirmado
                        )),
                    // FLUXO DE FALHA: Sem entregadores - Compensar TUDO (Pagamento + Restaurante)
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
                    await FinalizarCompensacaoSeCompleta(context);
                }),

            When(PedidoCanceladoRestaurante)
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

        // ==================== CONFIGURAÇÕES ADICIONAIS ====================

        // Configurar o que acontece quando a SAGA é finalizada
        SetCompletedWhenFinalized();
    }

    /// <summary>
    /// Verifica se todas as compensações necessárias foram executadas e finaliza a SAGA.
    /// </summary>
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
