using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.ServicoRestaurante.Servicos;

namespace SagaPoc.ServicoRestaurante.Consumers;

/// <summary>
/// Consumer responsável por cancelar pedidos de restaurante (compensação).
/// Recebe comando CancelarPedidoRestaurante como parte do fluxo de compensação da SAGA.
/// </summary>
public class CancelarPedidoRestauranteConsumer : IConsumer<CancelarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly ILogger<CancelarPedidoRestauranteConsumer> _logger;

    public CancelarPedidoRestauranteConsumer(
        IServicoRestaurante servico,
        ILogger<CancelarPedidoRestauranteConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CancelarPedidoRestaurante> context)
    {
        var mensagem = context.Message;

        _logger.LogWarning(
            "COMPENSAÇÃO: Recebido comando CancelarPedidoRestaurante. " +
            "CorrelacaoId: {CorrelacaoId}, RestauranteId: {RestauranteId}, PedidoId: {PedidoId}",
            mensagem.CorrelacaoId,
            mensagem.RestauranteId,
            mensagem.PedidoId
        );

        try
        {
            // Executar cancelamento do pedido
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

                // Mesmo que falhe, não vamos lançar exceção para não travar a SAGA
                // Em produção, isso deveria ser logado em um sistema de alertas
            }
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

            // Não re-throw - compensações devem ser idempotentes e tolerantes a falhas
            // Em produção, criar alerta para investigação manual
        }
    }
}
