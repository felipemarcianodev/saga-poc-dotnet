using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.ServicoPagamento.Servicos;

namespace SagaPoc.ServicoPagamento.Consumers;

/// <summary>
/// Consumer responsável por estornar pagamentos (compensação).
/// Recebe comando EstornarPagamento como parte do fluxo de compensação da SAGA.
/// </summary>
public class EstornarPagamentoConsumer : IConsumer<EstornarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly ILogger<EstornarPagamentoConsumer> _logger;

    public EstornarPagamentoConsumer(
        IServicoPagamento servico,
        ILogger<EstornarPagamentoConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var mensagem = context.Message;

        _logger.LogWarning(
            "COMPENSAÇÃO: Recebido comando EstornarPagamento. " +
            "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
            mensagem.CorrelacaoId,
            mensagem.TransacaoId
        );

        try
        {
            // Executar estorno do pagamento
            var resultado = await _servico.EstornarAsync(mensagem.TransacaoId);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "COMPENSAÇÃO: Pagamento estornado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                    mensagem.CorrelacaoId,
                    mensagem.TransacaoId
                );
            }
            else
            {
                _logger.LogError(
                    "COMPENSAÇÃO: Falha ao estornar pagamento. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    mensagem.TransacaoId,
                    resultado.Erro.Mensagem
                );

                // Mesmo que falhe, não vamos lançar exceção para não travar a SAGA
                // Em produção, isso deveria ser logado em um sistema de alertas
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "COMPENSAÇÃO: Erro crítico ao estornar pagamento. " +
                "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                mensagem.CorrelacaoId,
                mensagem.TransacaoId
            );

            // Não re-throw - compensações devem ser idempotentes e tolerantes a falhas
            // Em produção, criar alerta para investigação manual
        }
    }
}
