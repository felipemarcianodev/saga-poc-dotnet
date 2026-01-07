using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoPagamento.Servicos;

namespace SagaPoc.ServicoPagamento.Consumers;

/// <summary>
/// Consumer responsável por processar pagamentos.
/// Recebe comando ProcessarPagamento e responde com PagamentoProcessado.
/// </summary>
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly ILogger<ProcessarPagamentoConsumer> _logger;

    public ProcessarPagamentoConsumer(
        IServicoPagamento servico,
        ILogger<ProcessarPagamentoConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        var mensagem = context.Message;

        // ============ TIMEOUT POLICY ============
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Timeout de 10s
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken,
            cts.Token
        );

        _logger.LogInformation(
            "Recebido comando ProcessarPagamento. CorrelacaoId: {CorrelacaoId}, " +
            "ClienteId: {ClienteId}, Valor: {Valor:C}, FormaPagamento: {FormaPagamento}",
            mensagem.CorrelacaoId,
            mensagem.ClienteId,
            mensagem.ValorTotal,
            mensagem.FormaPagamento
        );

        try
        {
            // Executar processamento do pagamento
            var resultado = await _servico.ProcessarAsync(
                mensagem.ClienteId,
                mensagem.ValorTotal,
                mensagem.FormaPagamento,
                linkedCts.Token
            );

            // Preparar resposta baseada no resultado
            var resposta = resultado.Match(
                sucesso: dados => new PagamentoProcessado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Sucesso: true,
                    TransacaoId: dados.TransacaoId,
                    MotivoFalha: null
                ),
                falha: erro => new PagamentoProcessado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Sucesso: false,
                    TransacaoId: null,
                    MotivoFalha: erro.Mensagem
                )
            );

            // Enviar resposta
            await context.RespondAsync(resposta);

            _logger.LogInformation(
                "Resposta enviada. CorrelacaoId: {CorrelacaoId}, Sucesso: {Sucesso}, " +
                "TransacaoId: {TransacaoId}",
                mensagem.CorrelacaoId,
                resposta.Sucesso,
                resposta.TransacaoId
            );
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogError(
                "[TIMEOUT] Timeout ao processar pagamento. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta de falha por timeout
            await context.RespondAsync(new PagamentoProcessado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: null,
                MotivoFalha: "Timeout ao processar pagamento"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao processar ProcessarPagamento. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta de falha em caso de exceção inesperada
            await context.RespondAsync(new PagamentoProcessado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: null,
                MotivoFalha: "Erro interno ao processar pagamento"
            ));

            throw; // Re-throw para MassTransit lidar com retry policy
        }
    }
}
