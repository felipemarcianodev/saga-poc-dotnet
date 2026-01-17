using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.Common.Modelos;

namespace SagaPoc.Orquestrador.Sagas;

/// <summary>
/// SAGA que orquestra o processo completo de um pedido de delivery usando Rebus.
/// Implementa o padrão SAGA orquestrado com compensações.
/// </summary>
public class PedidoSaga : Saga<PedidoSagaData>,
    IAmInitiatedBy<IniciarPedido>,
    IHandleMessages<PedidoRestauranteValidado>,
    IHandleMessages<PagamentoProcessado>,
    IHandleMessages<EntregadorAlocado>,
    IHandleMessages<NotificacaoEnviada>,
    IHandleMessages<PedidoRestauranteCancelado>,
    IHandleMessages<PagamentoEstornado>,
    IHandleMessages<EntregadorLiberado>
{
    private readonly IBus _bus;
    private readonly ILogger<PedidoSaga> _logger;

    public PedidoSaga(IBus bus, ILogger<PedidoSaga> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Configura como correlacionar mensagens com instâncias da SAGA.
    /// </summary>
    protected override void CorrelateMessages(ICorrelationConfig<PedidoSagaData> config)
    {
        // Todas as mensagens são correlacionadas pelo CorrelacaoId
        config.Correlate<IniciarPedido>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<PedidoRestauranteValidado>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<PagamentoProcessado>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<EntregadorAlocado>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<NotificacaoEnviada>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<PedidoRestauranteCancelado>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<PagamentoEstornado>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<EntregadorLiberado>(m => m.CorrelacaoId, d => d.Id);
    }

    // ==================== HANDLER INICIAL ====================

    /// <summary>
    /// Inicia a SAGA quando um novo pedido é criado.
    /// </summary>
    public async Task Handle(IniciarPedido message)
    {
        if (!IsNew) return; // Evitar duplicação

        Data.ClienteId = message.ClienteId;
        Data.RestauranteId = message.RestauranteId;
        Data.EnderecoEntrega = message.EnderecoEntrega;
        Data.FormaPagamento = message.FormaPagamento;
        Data.DataInicio = DateTime.UtcNow;
        Data.EstadoAtual = "ValidandoRestaurante";

        _logger.LogInformation("[SAGA] Iniciando pedido {PedidoId} - Cliente: {ClienteId}",
            Data.Id, message.ClienteId);

        // Enviar comando para validar o pedido no restaurante
        await _bus.Send(new ValidarPedidoRestaurante(
            Data.Id,
            message.RestauranteId,
            message.Itens
        ));
    }

    // ==================== VALIDAÇÃO DO RESTAURANTE ====================

    /// <summary>
    /// Processa a resposta da validação do restaurante.
    /// </summary>
    public async Task Handle(PedidoRestauranteValidado message)
    {
        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Validação restaurante: {Status}",
            Data.Id, message.Valido ? "APROVADO" : "REJEITADO");

        if (message.Valido)
        {
            // FLUXO DE SUCESSO: Restaurante validou o pedido
            Data.ValorTotal = message.ValorTotal;
            Data.TempoPreparoMinutos = message.TempoPreparoMinutos;
            Data.TaxaEntrega = Data.ValorTotal * 0.15m; // 15% de taxa
            Data.RestauranteValidado = true; // Marcar para possível compensação
            Data.EstadoAtual = "ProcessandoPagamento";

            // Enviar comando para processar o pagamento
            await _bus.Send(new ProcessarPagamento(
                Data.Id,
                Data.ClienteId,
                Data.ValorTotal + Data.TaxaEntrega,
                Data.FormaPagamento
            ));
        }
        else
        {
            // FLUXO DE FALHA: Restaurante rejeitou o pedido
            Data.MotivoRejeicao = message.MotivoRejeicao;
            Data.DataConclusao = DateTime.UtcNow;
            Data.EstadoAtual = "Cancelado";

            _logger.LogWarning("[SAGA] Pedido {PedidoId} - Cancelado: {Motivo}",
                Data.Id, message.MotivoRejeicao);

            // Notificar cliente do cancelamento
            await _bus.Send(new NotificarCliente(
                Data.Id,
                Data.ClienteId,
                $"Pedido cancelado: {message.MotivoRejeicao}",
                TipoNotificacao.PedidoCancelado
            ));

            // Marcar SAGA como completa
            MarkAsComplete();
        }
    }

    // ==================== PROCESSAMENTO DO PAGAMENTO ====================

    /// <summary>
    /// Processa a resposta do processamento de pagamento.
    /// </summary>
    public async Task Handle(PagamentoProcessado message)
    {
        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Pagamento: {Status}",
            Data.Id, message.Sucesso ? "APROVADO" : "REJEITADO");

        if (message.Sucesso)
        {
            // FLUXO DE SUCESSO: Pagamento aprovado
            Data.TransacaoId = message.TransacaoId;
            Data.PagamentoProcessado = true; // Marcar para possível compensação
            Data.EstadoAtual = "AlocandoEntregador";

            // Enviar comando para alocar entregador
            await _bus.Send(new AlocarEntregador(
                Data.Id,
                Data.RestauranteId,
                Data.EnderecoEntrega,
                Data.TaxaEntrega
            ));
        }
        else
        {
            // FLUXO DE FALHA: Pagamento recusado - Iniciar compensação
            await IniciarCompensacao(message.MotivoFalha);

            // Compensar: Cancelar pedido no restaurante (se foi validado)
            if (Data.RestauranteValidado)
            {
                await _bus.Send(new CancelarPedidoRestaurante(
                    Data.Id,
                    Data.RestauranteId,
                    Data.Id
                ));
            }
            else
            {
                // Se não houve nada para compensar, finalizar
                await FinalizarCompensacao("Pagamento recusado");
            }
        }
    }

    // ==================== ALOCAÇÃO DO ENTREGADOR ====================

    /// <summary>
    /// Processa a resposta da alocação de entregador.
    /// </summary>
    public async Task Handle(EntregadorAlocado message)
    {
        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Entregador: {Status}",
            Data.Id, message.Alocado ? "ALOCADO" : "INDISPONÍVEL");

        if (message.Alocado)
        {
            // FLUXO DE SUCESSO: Entregador alocado
            Data.EntregadorId = message.EntregadorId;
            Data.TempoEntregaMinutos = message.TempoEstimadoMinutos;
            Data.EntregadorAlocado = true; // Marcar para possível compensação
            Data.EstadoAtual = "NotificandoCliente";

            _logger.LogInformation("[SAGA] Pedido {PedidoId} - Entregador {EntregadorId} alocado com sucesso!",
                Data.Id, message.EntregadorId);

            // Enviar notificação ao cliente
            await _bus.Send(new NotificarCliente(
                Data.Id,
                Data.ClienteId,
                $"Pedido confirmado! Entregador {message.EntregadorId} a caminho. Tempo estimado: {Data.TempoPreparoMinutos + Data.TempoEntregaMinutos} minutos.",
                TipoNotificacao.PedidoConfirmado
            ));
        }
        else
        {
            // FLUXO DE FALHA: Sem entregadores - Compensar TUDO (Pagamento + Restaurante)
            await IniciarCompensacao(message.MotivoFalha);

            // Compensações em ORDEM REVERSA

            // 1. Estornar pagamento (se foi processado)
            if (Data.PagamentoProcessado && !string.IsNullOrEmpty(Data.TransacaoId))
            {
                await _bus.Send(new EstornarPagamento(
                    Data.Id,
                    Data.TransacaoId
                ));
            }

            // 2. Cancelar no restaurante (se foi validado)
            if (Data.RestauranteValidado)
            {
                await _bus.Send(new CancelarPedidoRestaurante(
                    Data.Id,
                    Data.RestauranteId,
                    Data.Id
                ));
            }

            // Se não havia nada para compensar
            if (!Data.PagamentoProcessado && !Data.RestauranteValidado)
            {
                await FinalizarCompensacao("Entregador indisponível");
            }
        }
    }

    // ==================== NOTIFICAÇÃO AO CLIENTE ====================

    /// <summary>
    /// Processa a confirmação de envio de notificação.
    /// </summary>
    public async Task Handle(NotificacaoEnviada message)
    {
        if (Data.EstadoAtual == "NotificandoCliente")
        {
            // Pedido concluído com sucesso!
            Data.DataConclusao = DateTime.UtcNow;
            Data.EstadoAtual = "Concluido";

            var tempoTotal = (Data.DataConclusao.Value - Data.DataInicio).TotalSeconds;

            _logger.LogInformation("[SAGA] Pedido {PedidoId} - CONCLUÍDO COM SUCESSO!", Data.Id);
            _logger.LogInformation("[SAGA] Pedido {PedidoId} - Tempo total: {Tempo:F2}s", Data.Id, tempoTotal);
            _logger.LogInformation("[SAGA] Pedido {PedidoId} - Tempo estimado entrega: {Minutos} minutos",
                Data.Id, Data.TempoPreparoMinutos + Data.TempoEntregaMinutos);

            MarkAsComplete();
        }
        // Se estiver em compensação, a notificação já foi tratada
    }

    // ==================== HANDLERS DE COMPENSAÇÃO ====================

    /// <summary>
    /// Processa a confirmação de cancelamento no restaurante.
    /// </summary>
    public async Task Handle(PedidoRestauranteCancelado message)
    {
        Data.PassosCompensados.Add($"RestauranteCancelado:{DateTime.UtcNow}");

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Pedido cancelado no restaurante", Data.Id);

        await VerificarSeCompensacaoCompleta();
    }

    /// <summary>
    /// Processa a confirmação de estorno de pagamento.
    /// </summary>
    public async Task Handle(PagamentoEstornado message)
    {
        Data.PassosCompensados.Add($"PagamentoEstornado:{DateTime.UtcNow}");

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Pagamento estornado com sucesso", Data.Id);

        await VerificarSeCompensacaoCompleta();
    }

    /// <summary>
    /// Processa a confirmação de liberação de entregador.
    /// </summary>
    public async Task Handle(EntregadorLiberado message)
    {
        Data.PassosCompensados.Add($"EntregadorLiberado:{DateTime.UtcNow}");

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Entregador liberado", Data.Id);

        await VerificarSeCompensacaoCompleta();
    }

    // ==================== MÉTODOS AUXILIARES ====================

    /// <summary>
    /// Inicia o processo de compensação.
    /// </summary>
    private async Task IniciarCompensacao(string? motivo)
    {
        Data.MensagemErro = motivo;
        Data.EmCompensacao = true;
        Data.DataInicioCompensacao = DateTime.UtcNow;
        Data.EstadoAtual = "ExecutandoCompensacao";

        _logger.LogWarning("[SAGA] Pedido {PedidoId} - COMPENSAÇÃO INICIADA", Data.Id);
        _logger.LogWarning("[SAGA] Pedido {PedidoId} - Motivo: {Motivo}", Data.Id, motivo);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifica se todas as compensações necessárias foram executadas e finaliza a SAGA.
    /// </summary>
    private async Task VerificarSeCompensacaoCompleta()
    {
        if (!Data.EmCompensacao)
            return;

        // Verificar se todas as compensações necessárias foram executadas
        var todasCompensadas = true;

        if (Data.PagamentoProcessado &&
            !Data.PassosCompensados.Any(p => p.StartsWith("PagamentoEstornado")))
        {
            todasCompensadas = false;
        }

        if (Data.RestauranteValidado &&
            !Data.PassosCompensados.Any(p => p.StartsWith("RestauranteCancelado")))
        {
            todasCompensadas = false;
        }

        if (Data.EntregadorAlocado &&
            !Data.PassosCompensados.Any(p => p.StartsWith("EntregadorLiberado")))
        {
            todasCompensadas = false;
        }

        if (todasCompensadas)
        {
            await FinalizarCompensacao(Data.MensagemErro);
        }
    }

    /// <summary>
    /// Finaliza o processo de compensação e marca a SAGA como completa.
    /// </summary>
    private async Task FinalizarCompensacao(string? motivo)
    {
        Data.DataConclusaoCompensacao = DateTime.UtcNow;
        Data.DataConclusao = DateTime.UtcNow;
        Data.EstadoAtual = "Cancelado";

        if (Data.DataInicioCompensacao.HasValue)
        {
            var duracao = (Data.DataConclusaoCompensacao.Value - Data.DataInicioCompensacao.Value).TotalSeconds;

            _logger.LogInformation("[SAGA] Pedido {PedidoId} - COMPENSAÇÃO CONCLUÍDA ({Duracao:F2}s)",
                Data.Id, duracao);
            _logger.LogInformation("[SAGA] Pedido {PedidoId} - Passos compensados: {Total}",
                Data.Id, Data.PassosCompensados.Count);
        }

        // Notificar cliente sobre o cancelamento
        await _bus.Send(new NotificarCliente(
            Data.Id,
            Data.ClienteId,
            $"Pedido cancelado: {motivo}. Todos os valores foram estornados.",
            TipoNotificacao.PedidoCancelado
        ));

        // Marcar SAGA como completa
        MarkAsComplete();
    }
}
