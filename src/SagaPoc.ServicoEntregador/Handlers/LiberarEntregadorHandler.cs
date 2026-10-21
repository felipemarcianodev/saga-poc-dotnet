using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.ServicoEntregador.Servicos;
using SagaPoc.Infrastructure.Core;

namespace SagaPoc.ServicoEntregador.Handlers;

/// <summary>
/// Handler responsável por liberar entregadores (compensação).
/// Recebe comando LiberarEntregador como parte do fluxo de compensação da SAGA.
/// </summary>
public class LiberarEntregadorHandler : IHandleMessages<LiberarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly IRegistroCancelamentos _cancelamentos;
    private readonly IBus _bus;
    private readonly ILogger<LiberarEntregadorHandler> _logger;

    public LiberarEntregadorHandler(
        IServicoEntregador servico,
        IRepositorioIdempotencia idempotencia,
        IRegistroCancelamentos cancelamentos,
        IBus bus,
        ILogger<LiberarEntregadorHandler> logger)
    {
        _servico = servico;
        _idempotencia = idempotencia;
        _cancelamentos = cancelamentos;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(LiberarEntregador mensagem)
    {
        // Marcar antes de procurar o entregador: uma alocação em andamento registra o entregador e
        // depois confere o cancelamento, então pelo menos um dos dois lados vê o outro.
        _cancelamentos.MarcarCancelado(mensagem.CorrelacaoId);
        var entregadorId = mensagem.EntregadorId ?? _cancelamentos.ObterRecurso(mensagem.CorrelacaoId);

        if (entregadorId is null)
        {
            // Nada foi alocado ainda; a alocação que chegar depois será recusada.
            _logger.LogWarning(
                "[COMPENSAÇÃO] Nenhum entregador alocado para o pedido; alocação posterior bloqueada. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );
            await _bus.Reply(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                EntregadorId: null
            ));
            return;
        }

        var chaveIdempotencia = $"liberacao:{entregadorId}:{mensagem.CorrelacaoId}";

        _logger.LogWarning(
            "[COMPENSAÇÃO] Recebido comando LiberarEntregador. " +
            "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
            mensagem.CorrelacaoId,
            entregadorId
        );

        // ==================== IDEMPOTÊNCIA ====================
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[COMPENSAÇÃO] Liberação já processada anteriormente - EntregadorId: {EntregadorId}",
                entregadorId
            );

            await _bus.Reply(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                EntregadorId: entregadorId
            ));
            return;
        }

        try
        {
            // ==================== PROCESSAR LIBERAÇÃO ====================
            var resultado = await _servico.LiberarAsync(entregadorId);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "[COMPENSAÇÃO] Entregador liberado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
                    mensagem.CorrelacaoId,
                    entregadorId
                );

                await _idempotencia.MarcarProcessadoAsync(
                    chaveIdempotencia,
                    new { entregadorId = entregadorId, data = DateTime.UtcNow }
                );
            }
            else
            {
                _logger.LogError(
                    "[COMPENSAÇÃO] Falha ao liberar entregador. " +
                    "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    entregadorId,
                    resultado.Erro.Mensagem
                );
            }

            await _bus.Reply(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                EntregadorId: entregadorId
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[COMPENSAÇÃO] Erro crítico ao liberar entregador. " +
                "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
                mensagem.CorrelacaoId,
                entregadorId
            );

            await _bus.Reply(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: false,
                EntregadorId: entregadorId
            ));

            // Sem relançar: com exceção, o Rebus descartaria a resposta acima. A SAGA
            // recebe a falha e decide se tenta de novo.
        }
    }
}
