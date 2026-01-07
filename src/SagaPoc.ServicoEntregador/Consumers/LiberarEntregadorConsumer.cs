using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.ServicoEntregador.Servicos;

namespace SagaPoc.ServicoEntregador.Consumers;

/// <summary>
/// Consumer responsável por liberar entregadores (compensação).
/// Recebe comando LiberarEntregador como parte do fluxo de compensação da SAGA.
/// </summary>
public class LiberarEntregadorConsumer : IConsumer<LiberarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly ILogger<LiberarEntregadorConsumer> _logger;

    public LiberarEntregadorConsumer(
        IServicoEntregador servico,
        ILogger<LiberarEntregadorConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LiberarEntregador> context)
    {
        var mensagem = context.Message;

        _logger.LogWarning(
            "COMPENSAÇÃO: Recebido comando LiberarEntregador. " +
            "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
            mensagem.CorrelacaoId,
            mensagem.EntregadorId
        );

        try
        {
            // Executar liberação do entregador
            var resultado = await _servico.LiberarAsync(mensagem.EntregadorId);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "COMPENSAÇÃO: Entregador liberado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
                    mensagem.CorrelacaoId,
                    mensagem.EntregadorId
                );
            }
            else
            {
                _logger.LogError(
                    "COMPENSAÇÃO: Falha ao liberar entregador. " +
                    "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    mensagem.EntregadorId,
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
                "COMPENSAÇÃO: Erro crítico ao liberar entregador. " +
                "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
                mensagem.CorrelacaoId,
                mensagem.EntregadorId
            );

            // Não re-throw - compensações devem ser idempotentes e tolerantes a falhas
            // Em produção, criar alerta para investigação manual
        }
    }
}
