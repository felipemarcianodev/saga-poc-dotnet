using Microsoft.Extensions.Options;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.Common.Modelos;

namespace SagaPoc.Orquestrador.Sagas;

/// <summary>
/// SAGA que orquestra o processo completo de um pedido de delivery usando Rebus.
/// Implementa o padrão SAGA orquestrado com compensações, conferência do resultado de cada
/// compensação, novas tentativas e prazos para o pedido e para a compensação.
/// </summary>
public class PedidoSaga : Saga<PedidoSagaData>,
    IAmInitiatedBy<IniciarPedido>,
    IHandleMessages<PedidoRestauranteValidado>,
    IHandleMessages<PagamentoProcessado>,
    IHandleMessages<EntregadorAlocado>,
    IHandleMessages<NotificacaoEnviada>,
    IHandleMessages<PedidoRestauranteCancelado>,
    IHandleMessages<PagamentoEstornado>,
    IHandleMessages<EntregadorLiberado>,
    IHandleMessages<PrazoPedidoExpirado>,
    IHandleMessages<PrazoCompensacaoExpirado>
{
    // Estados gravados em PedidoSagaData.EstadoAtual
    private const string ValidandoRestaurante = "ValidandoRestaurante";
    private const string ProcessandoPagamento = "ProcessandoPagamento";
    private const string AlocandoEntregador = "AlocandoEntregador";
    private const string NotificandoCliente = "NotificandoCliente";
    private const string ExecutandoCompensacao = "ExecutandoCompensacao";

    // Passos que podem ser compensados
    private const string PassoRestaurante = "Restaurante";
    private const string PassoPagamento = "Pagamento";
    private const string PassoEntregador = "Entregador";

    private readonly IBus _bus;
    private readonly OpcoesSaga _opcoes;
    private readonly ILogger<PedidoSaga> _logger;

    public PedidoSaga(IBus bus, IOptions<OpcoesSaga> opcoes, ILogger<PedidoSaga> logger)
    {
        _bus = bus;
        _opcoes = opcoes.Value;
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
        config.Correlate<PrazoPedidoExpirado>(m => m.CorrelacaoId, d => d.Id);
        config.Correlate<PrazoCompensacaoExpirado>(m => m.CorrelacaoId, d => d.Id);
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
        Data.EstadoAtual = ValidandoRestaurante;

        _logger.LogInformation("[SAGA] Iniciando pedido {PedidoId} - Cliente: {ClienteId}",
            Data.Id, message.ClienteId);

        // Enviar comando para validar o pedido no restaurante
        await _bus.Send(new ValidarPedidoRestaurante(
            Data.Id,
            message.RestauranteId,
            message.Itens
        ));

        // Se algum serviço não responder, o prazo dispara a compensação
        await _bus.DeferLocal(
            TimeSpan.FromSeconds(_opcoes.PrazoPedidoSegundos),
            new PrazoPedidoExpirado(Data.Id));
    }

    // ==================== VALIDAÇÃO DO RESTAURANTE ====================

    /// <summary>
    /// Processa a resposta da validação do restaurante.
    /// </summary>
    public async Task Handle(PedidoRestauranteValidado message)
    {
        if (Data.EmCompensacao)
        {
            // Resposta atrasada: o restaurante aceitou depois do prazo, então o pedido lá precisa ser cancelado.
            if (message.Valido)
            {
                Data.RestauranteValidado = true;
                Data.PedidoRestauranteId = message.PedidoId;
                _logger.LogWarning("[SAGA] Pedido {PedidoId} - Validação do restaurante chegou durante a compensação; cancelando também",
                    Data.Id);
                await CompensarPasso(PassoRestaurante);
            }
            return;
        }

        if (!EstaNoEstado(ValidandoRestaurante, nameof(PedidoRestauranteValidado))) return;

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Validação restaurante: {Status}",
            Data.Id, message.Valido ? "APROVADO" : "REJEITADO");

        if (message.Valido)
        {
            // FLUXO DE SUCESSO: Restaurante validou o pedido
            Data.ValorTotal = message.ValorTotal;
            Data.TempoPreparoMinutos = message.TempoPreparoMinutos;
            Data.TaxaEntrega = Data.ValorTotal * 0.15m; // 15% de taxa
            Data.RestauranteValidado = true; // Marcar para possível compensação
            Data.PedidoRestauranteId = message.PedidoId;
            Data.EstadoAtual = ProcessandoPagamento;

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
        if (Data.EmCompensacao)
        {
            // Resposta atrasada: o pagamento foi aprovado depois do prazo, então precisa ser estornado.
            if (message.Sucesso && !string.IsNullOrEmpty(message.TransacaoId))
            {
                Data.PagamentoProcessado = true;
                Data.TransacaoId = message.TransacaoId;
                _logger.LogWarning("[SAGA] Pedido {PedidoId} - Pagamento {TransacaoId} aprovado durante a compensação; estornando também",
                    Data.Id, message.TransacaoId);
                await CompensarPasso(PassoPagamento);
            }
            return;
        }

        if (!EstaNoEstado(ProcessandoPagamento, nameof(PagamentoProcessado))) return;

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Pagamento: {Status}",
            Data.Id, message.Sucesso ? "APROVADO" : "REJEITADO");

        if (message.Sucesso)
        {
            // FLUXO DE SUCESSO: Pagamento aprovado
            Data.TransacaoId = message.TransacaoId;
            Data.PagamentoProcessado = true; // Marcar para possível compensação
            Data.EstadoAtual = AlocandoEntregador;

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
            // FLUXO DE FALHA: Pagamento recusado - compensar o restaurante
            await IniciarCompensacao(message.MotivoFalha);
        }
    }

    // ==================== ALOCAÇÃO DO ENTREGADOR ====================

    /// <summary>
    /// Processa a resposta da alocação de entregador.
    /// </summary>
    public async Task Handle(EntregadorAlocado message)
    {
        if (Data.EmCompensacao)
        {
            // Resposta atrasada: o entregador foi alocado depois do prazo, então precisa ser liberado.
            if (message.Alocado && !string.IsNullOrEmpty(message.EntregadorId))
            {
                Data.EntregadorAlocado = true;
                Data.EntregadorId = message.EntregadorId;
                _logger.LogWarning("[SAGA] Pedido {PedidoId} - Entregador {EntregadorId} alocado durante a compensação; liberando também",
                    Data.Id, message.EntregadorId);
                await CompensarPasso(PassoEntregador);
            }
            return;
        }

        if (!EstaNoEstado(AlocandoEntregador, nameof(EntregadorAlocado))) return;

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Entregador: {Status}",
            Data.Id, message.Alocado ? "ALOCADO" : "INDISPONÍVEL");

        if (message.Alocado)
        {
            // FLUXO DE SUCESSO: Entregador alocado
            Data.EntregadorId = message.EntregadorId;
            Data.TempoEntregaMinutos = message.TempoEstimadoMinutos;
            Data.EntregadorAlocado = true; // Marcar para possível compensação
            Data.EstadoAtual = NotificandoCliente;

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
            // FLUXO DE FALHA: Sem entregadores - compensar pagamento e restaurante
            await IniciarCompensacao(message.MotivoFalha);
        }
    }

    // ==================== NOTIFICAÇÃO AO CLIENTE ====================

    /// <summary>
    /// Processa a confirmação de envio de notificação. A notificação é best effort:
    /// mesmo sem envio, o pedido já está confirmado.
    /// </summary>
    public Task Handle(NotificacaoEnviada message)
    {
        // Notificação de cancelamento chega depois de a SAGA terminar; só a de confirmação conclui o pedido.
        if (Data.EstadoAtual != NotificandoCliente) return Task.CompletedTask;

        if (!message.Enviada)
        {
            _logger.LogWarning("[SAGA] Pedido {PedidoId} - Notificação ao cliente não foi enviada (não bloqueia o pedido)",
                Data.Id);
        }

        ConcluirComSucesso();
        return Task.CompletedTask;
    }

    // ==================== HANDLERS DE COMPENSAÇÃO ====================

    /// <summary>
    /// Processa a resposta do cancelamento no restaurante.
    /// </summary>
    public Task Handle(PedidoRestauranteCancelado message) =>
        RegistrarResultadoCompensacao(PassoRestaurante, message.Sucesso);

    /// <summary>
    /// Processa a resposta do estorno de pagamento.
    /// </summary>
    public Task Handle(PagamentoEstornado message) =>
        RegistrarResultadoCompensacao(PassoPagamento, message.Sucesso);

    /// <summary>
    /// Processa a resposta da liberação de entregador.
    /// </summary>
    public Task Handle(EntregadorLiberado message) =>
        RegistrarResultadoCompensacao(PassoEntregador, message.Sucesso);

    // ==================== PRAZOS ====================

    /// <summary>
    /// O pedido não terminou dentro do prazo: algum serviço não respondeu.
    /// </summary>
    public async Task Handle(PrazoPedidoExpirado message)
    {
        if (Data.EmCompensacao) return; // a compensação tem prazo próprio

        _logger.LogWarning("[SAGA] Pedido {PedidoId} - PRAZO EXPIRADO na etapa {Etapa} ({Prazo}s)",
            Data.Id, Data.EstadoAtual, _opcoes.PrazoPedidoSegundos);

        if (Data.EstadoAtual == NotificandoCliente)
        {
            // Entregador já alocado: o pedido está confirmado, falta só a resposta da notificação.
            _logger.LogWarning("[SAGA] Pedido {PedidoId} - Notificação sem resposta no prazo (não bloqueia o pedido)",
                Data.Id);
            ConcluirComSucesso();
            return;
        }

        Data.PrazoExpirado = true;
        await IniciarCompensacao(
            $"Prazo de {_opcoes.PrazoPedidoSegundos}s esgotado na etapa {Data.EstadoAtual}",
            etapaSemResposta: Data.EstadoAtual);
    }

    /// <summary>
    /// A compensação não terminou dentro do prazo: os passos sem resposta contam como falha.
    /// </summary>
    public async Task Handle(PrazoCompensacaoExpirado message)
    {
        if (!Data.EmCompensacao || Data.PassosPendentes.Count == 0) return;

        foreach (var passo in Data.PassosPendentes)
        {
            Data.PassosComFalha.Add(passo);
            Data.ErrosCompensacao.Add($"{passo}: sem resposta em {_opcoes.PrazoCompensacaoSegundos}s");
        }

        _logger.LogError("[SAGA] Pedido {PedidoId} - Compensação sem resposta no prazo ({Prazo}s): {Passos}",
            Data.Id, _opcoes.PrazoCompensacaoSegundos, string.Join(", ", Data.PassosPendentes));

        Data.PassosPendentes.Clear();
        await FinalizarCompensacao();
    }

    // ==================== MÉTODOS AUXILIARES ====================

    /// <summary>
    /// Confere se a resposta é da etapa em que a SAGA está; resposta repetida ou fora de ordem é ignorada.
    /// </summary>
    private bool EstaNoEstado(string estadoEsperado, string mensagem)
    {
        if (Data.EstadoAtual == estadoEsperado) return true;

        _logger.LogWarning("[SAGA] Pedido {PedidoId} - {Mensagem} ignorada no estado {Estado} (esperado {Esperado})",
            Data.Id, mensagem, Data.EstadoAtual, estadoEsperado);
        return false;
    }

    private void ConcluirComSucesso()
    {
        Data.DataConclusao = DateTime.UtcNow;
        Data.EstadoAtual = "Concluido";

        var tempoTotal = (Data.DataConclusao.Value - Data.DataInicio).TotalSeconds;

        _logger.LogInformation("[SAGA] Pedido {PedidoId} - CONCLUÍDO COM SUCESSO!", Data.Id);
        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Tempo total: {Tempo:F2}s", Data.Id, tempoTotal);
        _logger.LogInformation("[SAGA] Pedido {PedidoId} - Tempo estimado entrega: {Minutos} minutos",
            Data.Id, Data.TempoPreparoMinutos + Data.TempoEntregaMinutos);

        MarkAsComplete();
    }

    /// <summary>
    /// Inicia a compensação dos passos já feitos, na ordem inversa em que foram executados.
    /// Com prazo esgotado, compensa também o passo que não respondeu: o serviço cancela pelo
    /// pedido (CorrelacaoId), desfaz o que já tiver feito e recusa o que chegar depois.
    /// </summary>
    private async Task IniciarCompensacao(string? motivo, string? etapaSemResposta = null)
    {
        Data.MensagemErro = motivo;
        Data.EmCompensacao = true;
        Data.DataInicioCompensacao = DateTime.UtcNow;
        Data.EstadoAtual = ExecutandoCompensacao;

        _logger.LogWarning("[SAGA] Pedido {PedidoId} - COMPENSAÇÃO INICIADA", Data.Id);
        _logger.LogWarning("[SAGA] Pedido {PedidoId} - Motivo: {Motivo}", Data.Id, motivo);

        await _bus.DeferLocal(
            TimeSpan.FromSeconds(_opcoes.PrazoCompensacaoSegundos),
            new PrazoCompensacaoExpirado(Data.Id));

        if (Data.EntregadorAlocado || etapaSemResposta == AlocandoEntregador)
            await CompensarPasso(PassoEntregador);
        if (Data.PagamentoProcessado || etapaSemResposta == ProcessandoPagamento)
            await CompensarPasso(PassoPagamento);
        if (Data.RestauranteValidado || etapaSemResposta == ValidandoRestaurante)
            await CompensarPasso(PassoRestaurante);

        await VerificarSeCompensacaoCompleta();
    }

    /// <summary>
    /// Envia a primeira tentativa de compensação de um passo; um passo já compensado ou pendente não é reenviado.
    /// </summary>
    private async Task CompensarPasso(string passo)
    {
        if (Data.PassosPendentes.Contains(passo) ||
            Data.PassosCompensados.Contains(passo) ||
            Data.PassosComFalha.Contains(passo))
            return;

        Data.PassosPendentes.Add(passo);
        Data.TentativasPorPasso[passo] = 1;
        await EnviarComandoCompensacao(passo, atraso: null);
    }

    private Task EnviarComandoCompensacao(string passo, TimeSpan? atraso)
    {
        object comando = passo switch
        {
            // Sem o identificador (a resposta não chegou), o serviço compensa pelo CorrelacaoId
            PassoEntregador => new LiberarEntregador(Data.Id, Data.EntregadorId),
            PassoPagamento => new EstornarPagamento(Data.Id, Data.TransacaoId),
            PassoRestaurante => new CancelarPedidoRestaurante(Data.Id, Data.RestauranteId, Data.PedidoRestauranteId ?? Data.Id),
            _ => throw new InvalidOperationException($"Passo de compensação desconhecido: {passo}")
        };

        return atraso is null ? _bus.Send(comando) : _bus.Defer(atraso.Value, comando);
    }

    /// <summary>
    /// Registra a resposta de uma compensação. Recusa gera nova tentativa até o limite;
    /// esgotado o limite, o passo conta como falha.
    /// </summary>
    private async Task RegistrarResultadoCompensacao(string passo, bool sucesso)
    {
        if (!Data.PassosPendentes.Contains(passo))
        {
            _logger.LogWarning("[SAGA] Pedido {PedidoId} - Resposta de compensação de {Passo} ignorada (passo não pendente)",
                Data.Id, passo);
            return;
        }

        var tentativa = Data.TentativasPorPasso.GetValueOrDefault(passo, 1);

        if (sucesso)
        {
            Data.PassosPendentes.Remove(passo);
            Data.PassosCompensados.Add(passo);
            _logger.LogInformation("[SAGA] Pedido {PedidoId} - Compensação de {Passo} confirmada (tentativa {Tentativa})",
                Data.Id, passo, tentativa);
        }
        else
        {
            Data.ErrosCompensacao.Add($"{passo}: tentativa {tentativa} recusada");

            if (tentativa < _opcoes.MaximoTentativasCompensacao)
            {
                Data.TentativasPorPasso[passo] = tentativa + 1;
                _logger.LogWarning("[SAGA] Pedido {PedidoId} - Compensação de {Passo} recusada (tentativa {Tentativa} de {Maximo}); repetindo em {Intervalo}s",
                    Data.Id, passo, tentativa, _opcoes.MaximoTentativasCompensacao, _opcoes.IntervaloTentativasSegundos);
                await EnviarComandoCompensacao(passo, TimeSpan.FromSeconds(_opcoes.IntervaloTentativasSegundos));
                return;
            }

            Data.PassosPendentes.Remove(passo);
            Data.PassosComFalha.Add(passo);
            _logger.LogError("[SAGA] Pedido {PedidoId} - Compensação de {Passo} recusada {Tentativa} vezes; passo sem compensação",
                Data.Id, passo, tentativa);
        }

        await VerificarSeCompensacaoCompleta();
    }

    /// <summary>
    /// Finaliza a SAGA quando nenhum passo de compensação está esperando resposta.
    /// </summary>
    private async Task VerificarSeCompensacaoCompleta()
    {
        if (Data.EmCompensacao && Data.PassosPendentes.Count == 0)
        {
            await FinalizarCompensacao();
        }
    }

    /// <summary>
    /// Finaliza a compensação, notifica o cliente e marca a SAGA como completa.
    /// </summary>
    private async Task FinalizarCompensacao()
    {
        Data.DataConclusaoCompensacao = DateTime.UtcNow;
        Data.DataConclusao = DateTime.UtcNow;

        var duracao = (Data.DataConclusaoCompensacao.Value - Data.DataInicioCompensacao!.Value).TotalSeconds;
        string mensagemCliente;

        if (Data.PassosComFalha.Count == 0)
        {
            Data.EstadoAtual = "Cancelado";
            _logger.LogInformation("[SAGA] Pedido {PedidoId} - COMPENSAÇÃO CONCLUÍDA ({Duracao:F2}s)",
                Data.Id, duracao);
            _logger.LogInformation("[SAGA] Pedido {PedidoId} - Passos compensados: {Total}",
                Data.Id, Data.PassosCompensados.Count);

            mensagemCliente = Data.PagamentoProcessado
                ? $"Pedido cancelado: {Data.MensagemErro}. O valor pago foi estornado."
                : $"Pedido cancelado: {Data.MensagemErro}.";
        }
        else
        {
            // O desfecho não fica no banco (a SAGA concluída é removida); este log é o registro da pendência.
            Data.EstadoAtual = "CompensacaoFalhou";
            _logger.LogError("[SAGA] Pedido {PedidoId} - COMPENSAÇÃO FALHOU ({Duracao:F2}s). Sem compensação: {PassosComFalha}. Compensados: {PassosCompensados}. Erros: {Erros}. Exige intervenção manual",
                Data.Id, duracao,
                string.Join(", ", Data.PassosComFalha),
                string.Join(", ", Data.PassosCompensados),
                string.Join("; ", Data.ErrosCompensacao));

            mensagemCliente = $"Pedido cancelado: {Data.MensagemErro}. O cancelamento está em análise e você será avisado quando terminar.";
        }

        // Notificar cliente sobre o cancelamento
        await _bus.Send(new NotificarCliente(
            Data.Id,
            Data.ClienteId,
            mensagemCliente,
            TipoNotificacao.PedidoCancelado
        ));

        // Marcar SAGA como completa
        MarkAsComplete();
    }
}
