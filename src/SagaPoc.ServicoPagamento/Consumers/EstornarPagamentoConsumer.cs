using MassTransit;
using SagaPoc.Shared.Infraestrutura;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoPagamento.Servicos;

namespace SagaPoc.ServicoPagamento.Consumers;

/// <summary>
/// Consumer responsável por estornar pagamentos (compensação).
/// Recebe comando EstornarPagamento como parte do fluxo de compensação da SAGA.
/// </summary>
public class EstornarPagamentoConsumer : IConsumer<EstornarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<EstornarPagamentoConsumer> _logger;

    public EstornarPagamentoConsumer(
        IServicoPagamento servico,
        IRepositorioIdempotencia idempotencia,
        ILogger<EstornarPagamentoConsumer> logger)
    {
        _servico = servico;
        _idempotencia = idempotencia;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var mensagem = context.Message;
        var chaveIdempotencia = $"estorno:{mensagem.TransacaoId}";

        _logger.LogWarning(
            "COMPENSAÇÃO: Recebido comando EstornarPagamento. " +
            "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
            mensagem.CorrelacaoId,
            mensagem.TransacaoId
        );

        // ==================== IDEMPOTÊNCIA ====================
        // Verificar se já foi estornado
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "COMPENSAÇÃO: Estorno já processado anteriormente - TransacaoId: {TransacaoId}",
                mensagem.TransacaoId
            );

            // Responder com sucesso mesmo assim (idempotência)
            await context.Publish(new PagamentoEstornado(
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
                    "COMPENSAÇÃO: Pagamento estornado com sucesso. " +
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
                    "COMPENSAÇÃO: Falha ao estornar pagamento. " +
                    "CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    mensagem.TransacaoId,
                    resultado.Erro.Mensagem
                );
            }

            // Publicar resposta
            await context.Publish(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                TransacaoId: mensagem.TransacaoId
            ));
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

            // Publicar resposta de falha
            await context.Publish(new PagamentoEstornado(
                mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: mensagem.TransacaoId
            ));
        }
    }
}
