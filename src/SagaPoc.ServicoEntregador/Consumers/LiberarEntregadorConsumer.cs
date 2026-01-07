using MassTransit;
using SagaPoc.Shared.Infraestrutura;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoEntregador.Servicos;

namespace SagaPoc.ServicoEntregador.Consumers;

/// <summary>
/// Consumer responsável por liberar entregadores (compensação).
/// Recebe comando LiberarEntregador como parte do fluxo de compensação da SAGA.
/// </summary>
public class LiberarEntregadorConsumer : IConsumer<LiberarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<LiberarEntregadorConsumer> _logger;

    public LiberarEntregadorConsumer(
        IServicoEntregador servico,
        IRepositorioIdempotencia idempotencia,
        ILogger<LiberarEntregadorConsumer> logger)
    {
        _servico = servico;
        _idempotencia = idempotencia;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LiberarEntregador> context)
    {
        var mensagem = context.Message;
        var chaveIdempotencia = $"liberacao:{mensagem.EntregadorId}:{mensagem.CorrelacaoId}";

        _logger.LogWarning(
            "COMPENSAÇÃO: Recebido comando LiberarEntregador. " +
            "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
            mensagem.CorrelacaoId,
            mensagem.EntregadorId
        );

        // ==================== IDEMPOTÊNCIA ====================
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "COMPENSAÇÃO: Liberação já processada anteriormente - EntregadorId: {EntregadorId}",
                mensagem.EntregadorId
            );

            await context.Publish(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                EntregadorId: mensagem.EntregadorId
            ));
            return;
        }

        try
        {
            // ==================== PROCESSAR LIBERAÇÃO ====================
            var resultado = await _servico.LiberarAsync(mensagem.EntregadorId);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "COMPENSAÇÃO: Entregador liberado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
                    mensagem.CorrelacaoId,
                    mensagem.EntregadorId
                );

                await _idempotencia.MarcarProcessadoAsync(
                    chaveIdempotencia,
                    new { entregadorId = mensagem.EntregadorId, data = DateTime.UtcNow }
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
            }

            await context.Publish(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                EntregadorId: mensagem.EntregadorId
            ));
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

            await context.Publish(new EntregadorLiberado(
                mensagem.CorrelacaoId,
                Sucesso: false,
                EntregadorId: mensagem.EntregadorId
            ));
        }
    }
}
