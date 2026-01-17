using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.ServicoPagamento.Servicos;
using SagaPoc.Infrastructure.Core;

namespace SagaPoc.ServicoPagamento.Handlers;

/// <summary>
/// Handler responsável por estornar pagamentos (compensação).
/// Recebe comando EstornarPagamento como parte do fluxo de compensação da SAGA.
/// </summary>
public class EstornarPagamentoHandler : IHandleMessages<EstornarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly IBus _bus;
    private readonly ILogger<EstornarPagamentoHandler> _logger;

    public EstornarPagamentoHandler(
        IServicoPagamento servico,
        IRepositorioIdempotencia idempotencia,
        IBus bus,
        ILogger<EstornarPagamentoHandler> logger)
    {
        _servico = servico;
        _idempotencia = idempotencia;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(EstornarPagamento mensagem)
    {
        var chaveIdempotencia = $"estorno:{mensagem.TransacaoId}";

        _logger.LogWarning(
            "[COMPENSAÇÃO] Recebido comando EstornarPagamento. " +
            "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
            mensagem.CorrelacaoId,
            mensagem.TransacaoId
        );

        // ==================== IDEMPOTÊNCIA ====================
        // Verificar se já foi estornado
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[COMPENSAÇÃO] Estorno já processado anteriormente - TransacaoId: {TransacaoId}",
                mensagem.TransacaoId
            );

            // Responder com sucesso mesmo assim (idempotência)
            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                TransacaoId: mensagem.TransacaoId
            ));
            return;
        }

        try
        {
            // ==================== PROCESSAR ESTORNO ====================
            var resultado = await _servico.EstornarAsync(mensagem.TransacaoId);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "[COMPENSAÇÃO] Pagamento estornado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                    mensagem.CorrelacaoId,
                    mensagem.TransacaoId
                );

                // Marcar como processado
                await _idempotencia.MarcarProcessadoAsync(
                    chaveIdempotencia,
                    new { transacaoId = mensagem.TransacaoId, data = DateTime.UtcNow }
                );
            }
            else
            {
                _logger.LogError(
                    "[COMPENSAÇÃO] Falha ao estornar pagamento. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    mensagem.TransacaoId,
                    resultado.Erro.Mensagem
                );
            }

            // Enviar resposta usando Rebus
            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                TransacaoId: mensagem.TransacaoId
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[COMPENSAÇÃO] Erro crítico ao estornar pagamento. " +
                "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                mensagem.CorrelacaoId,
                mensagem.TransacaoId
            );

            // Enviar resposta de falha usando Rebus
            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: mensagem.TransacaoId
            ));

            throw; // Re-throw para Rebus lidar com retry policy
        }
    }
}
