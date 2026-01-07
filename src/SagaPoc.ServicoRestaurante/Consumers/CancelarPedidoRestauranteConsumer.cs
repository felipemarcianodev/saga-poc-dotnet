using MassTransit;
using SagaPoc.Shared.Infraestrutura;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoRestaurante.Servicos;

namespace SagaPoc.ServicoRestaurante.Consumers;

/// <summary>
/// Consumer responsável por cancelar pedidos de restaurante (compensação).
/// Recebe comando CancelarPedidoRestaurante como parte do fluxo de compensação da SAGA.
/// </summary>
public class CancelarPedidoRestauranteConsumer : IConsumer<CancelarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<CancelarPedidoRestauranteConsumer> _logger;

    public CancelarPedidoRestauranteConsumer(
        IServicoRestaurante servico,
        IRepositorioIdempotencia idempotencia,
        ILogger<CancelarPedidoRestauranteConsumer> logger)
    {
        _servico = servico;
        _idempotencia = idempotencia;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CancelarPedidoRestaurante> context)
    {
        var mensagem = context.Message;
        var chaveIdempotencia = $"cancelamento:{mensagem.PedidoId}";

        _logger.LogWarning(
            "COMPENSAÇÃO: Recebido comando CancelarPedidoRestaurante. " +
            "CorrelacaoId: {CorrelacaoId}, RestauranteId: {RestauranteId}, PedidoId: {PedidoId}",
            mensagem.CorrelacaoId,
            mensagem.RestauranteId,
            mensagem.PedidoId
        );

        // ==================== IDEMPOTÊNCIA ====================
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "COMPENSAÇÃO: Cancelamento já processado anteriormente - PedidoId: {PedidoId}",
                mensagem.PedidoId
            );

            await context.Publish(new PedidoRestauranteCancelado(
                mensagem.CorrelacaoId,
                Sucesso: true,
                PedidoId: mensagem.PedidoId
            ));
            return;
        }

        try
        {
            // ==================== PROCESSAR CANCELAMENTO ====================
            var resultado = await _servico.CancelarPedidoAsync(
                mensagem.RestauranteId,
                mensagem.PedidoId
            );

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "COMPENSAÇÃO: Pedido cancelado com sucesso. " +
                    "CorrelacaoId: {CorrelacaoId}, PedidoId: {PedidoId}",
                    mensagem.CorrelacaoId,
                    mensagem.PedidoId
                );

                await _idempotencia.MarcarProcessadoAsync(
                    chaveIdempotencia,
                    new { pedidoId = mensagem.PedidoId, data = DateTime.UtcNow }
                );
            }
            else
            {
                _logger.LogError(
                    "COMPENSAÇÃO: Falha ao cancelar pedido. " +
                    "CorrelacaoId: {CorrelacaoId}, PedidoId: {PedidoId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    mensagem.PedidoId,
                    resultado.Erro.Mensagem
                );
            }

            await context.Publish(new PedidoRestauranteCancelado(
                mensagem.CorrelacaoId,
                Sucesso: resultado.EhSucesso,
                PedidoId: mensagem.PedidoId
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "COMPENSAÇÃO: Erro crítico ao cancelar pedido. " +
                "CorrelacaoId: {CorrelacaoId}, PedidoId: {PedidoId}",
                mensagem.CorrelacaoId,
                mensagem.PedidoId
            );

            await context.Publish(new PedidoRestauranteCancelado(
                mensagem.CorrelacaoId,
                Sucesso: false,
                PedidoId: mensagem.PedidoId
            ));
        }
    }
}
