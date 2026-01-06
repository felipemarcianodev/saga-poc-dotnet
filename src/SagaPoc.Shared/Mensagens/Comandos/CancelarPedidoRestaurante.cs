namespace SagaPoc.Shared.Mensagens.Comandos;

/// <summary>
/// Comando de compensação para cancelar um pedido no restaurante.
/// Executado quando a SAGA falha após validar o pedido.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="RestauranteId">Identificador do restaurante.</param>
/// <param name="PedidoId">Identificador do pedido a ser cancelado.</param>
public record CancelarPedidoRestaurante(
    Guid CorrelacaoId,
    string RestauranteId,
    Guid PedidoId
);
