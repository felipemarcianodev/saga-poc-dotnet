using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Domain.Respostas;

namespace SagaPoc.FluxoCaixa.Lancamentos.Handlers;

/// <summary>
/// Handler para consumir mensagens de LancamentoRegistradoComSucesso
/// que podem estar em filas antigas. Este handler apenas descarta as mensagens.
/// </summary>
public class LancamentoRegistradoComSucessoHandler : IHandleMessages<LancamentoRegistradoComSucesso>
{
    private readonly ILogger<LancamentoRegistradoComSucessoHandler> _logger;

    public LancamentoRegistradoComSucessoHandler(ILogger<LancamentoRegistradoComSucessoHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(LancamentoRegistradoComSucesso message)
    {
        _logger.LogInformation(
            "Mensagem LancamentoRegistradoComSucesso recebida e descartada. " +
            "CorrelationId: {CorrelationId}, LancamentoId: {LancamentoId}",
            message.CorrelationId,
            message.LancamentoId);

        // Não faz nada, apenas consome a mensagem para limpar a fila
        return Task.CompletedTask;
    }
}
