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
/// Sem TransacaoId, estorna o que tiver sido cobrado para o pedido; em qualquer caso,
/// marca o pedido como cancelado para que uma cobrança atrasada seja recusada ou desfeita.
/// </summary>
public class EstornarPagamentoHandler : IHandleMessages<EstornarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly IRegistroCancelamentos _cancelamentos;
    private readonly IBus _bus;
    private readonly ILogger<EstornarPagamentoHandler> _logger;

    public EstornarPagamentoHandler(
        IServicoPagamento servico,
        IRepositorioIdempotencia idempotencia,
        IRegistroCancelamentos cancelamentos,
        IBus bus,
        ILogger<EstornarPagamentoHandler> logger)
    {
        _servico = servico;
        _idempotencia = idempotencia;
        _cancelamentos = cancelamentos;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(EstornarPagamento mensagem)
    {
        // Marcar antes de procurar a transação: uma cobrança em andamento registra a transação e
        // depois confere o cancelamento, então pelo menos um dos dois lados vê o outro.
        _cancelamentos.MarcarCancelado(mensagem.CorrelacaoId);
        var transacaoId = mensagem.TransacaoId ?? _cancelamentos.ObterRecurso(mensagem.CorrelacaoId);

        _logger.LogWarning(
            "[COMPENSAÇÃO] Recebido comando EstornarPagamento. " +
            "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
            mensagem.CorrelacaoId,
            transacaoId ?? "(nenhuma)"
        );

        if (transacaoId is null)
        {
            // Nada foi cobrado ainda; a cobrança que chegar depois será recusada.
            _logger.LogWarning(
                "[COMPENSAÇÃO] Nenhuma cobrança para o pedido; cobrança posterior bloqueada. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                TransacaoId: null
            ));
            return;
        }

        var chaveIdempotencia = $"estorno:{transacaoId}";

        // ==================== IDEMPOTÊNCIA ====================
        // Verificar se já foi estornado
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[COMPENSAÇÃO] Estorno já processado anteriormente - TransacaoId: {TransacaoId}",
                transacaoId
            );

            // Responder com sucesso mesmo assim (idempotência)
            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                TransacaoId: transacaoId
            ));
            return;
        }

        try
        {
            // ==================== PROCESSAR ESTORNO ====================
            var resultado = await _servico.EstornarAsync(transacaoId);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "[COMPENSAÇÃO] Pagamento estornado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                    mensagem.CorrelacaoId,
                    transacaoId
                );

                // Marcar como processado
                await _idempotencia.MarcarProcessadoAsync(
                    chaveIdempotencia,
                    new { transacaoId, data = DateTime.UtcNow }
                );
            }
            else
            {
                _logger.LogError(
                    "[COMPENSAÇÃO] Falha ao estornar pagamento. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    transacaoId,
                    resultado.Erro.Mensagem
                );
            }

            // Enviar resposta usando Rebus
            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                TransacaoId: transacaoId
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[COMPENSAÇÃO] Erro crítico ao estornar pagamento. " +
                "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                mensagem.CorrelacaoId,
                transacaoId
            );

            // Enviar resposta de falha usando Rebus
            await _bus.Reply(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: transacaoId
            ));

            // Sem relançar: com exceção, o Rebus descartaria a resposta acima. A SAGA
            // recebe a falha e decide se tenta de novo.
        }
    }
}
