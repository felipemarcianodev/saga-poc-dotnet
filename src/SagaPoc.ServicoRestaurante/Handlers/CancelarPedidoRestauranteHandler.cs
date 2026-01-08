using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoRestaurante.Servicos;

namespace SagaPoc.ServicoRestaurante.Handlers;

/// <summary>
/// Handler responsável por cancelar pedidos no restaurante (operação de compensação).
/// Recebe comando CancelarPedidoRestaurante e responde com PedidoRestauranteCancelado.
/// </summary>
public class CancelarPedidoRestauranteHandler : IHandleMessages<CancelarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly IBus _bus;
    private readonly ILogger<CancelarPedidoRestauranteHandler> _logger;

    public CancelarPedidoRestauranteHandler(
        IServicoRestaurante servico,
        IBus bus,
        ILogger<CancelarPedidoRestauranteHandler> logger)
    {
        _servico = servico;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(CancelarPedidoRestaurante mensagem)
    {
        _logger.LogInformation(
            "[COMPENSAÇÃO] Recebido comando CancelarPedidoRestaurante. " +
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

            // Usar Match() para tratar sucesso/falha de forma elegante (Result Pattern)
            resultado.Match(
                sucesso: _ =>
                {
                    _logger.LogInformation(
                        "[COMPENSAÇÃO] Pedido {PedidoId} cancelado com sucesso no restaurante",
                        mensagem.PedidoId
                    );
                },
                falha: erro =>
                {
                    _logger.LogError(
                        "[COMPENSAÇÃO] Erro ao cancelar pedido {PedidoId} - " +
                        "TipoErro: {TipoErro}, Codigo: {Codigo}, Motivo: {Motivo}",
                        mensagem.PedidoId,
                        erro.Tipo,
                        erro.Codigo,
                        erro.Mensagem
                    );
                }
            );

            // Preparar resposta
            var resposta = resultado.Match(
                sucesso: _ => new PedidoRestauranteCancelado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Sucesso: true,
                    PedidoId: mensagem.PedidoId
                ),
                falha: erro => new PedidoRestauranteCancelado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Sucesso: false,
                    PedidoId: mensagem.PedidoId
                )
            );

            // Enviar resposta de volta para o orquestrador
            await _bus.Reply(resposta);

            _logger.LogInformation(
                "[COMPENSAÇÃO] Resposta enviada. CorrelacaoId: {CorrelacaoId}, Sucesso: {Sucesso}",
                mensagem.CorrelacaoId,
                resposta.Sucesso
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[COMPENSAÇÃO] Erro ao processar CancelarPedidoRestaurante. " +
                "CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta de falha em caso de exceção inesperada
            await _bus.Reply(new PedidoRestauranteCancelado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Sucesso: false,
                PedidoId: mensagem.PedidoId
            ));

            throw; // Re-throw para Rebus lidar com retry policy
        }
    }
}
