using SagaPoc.Shared.Modelos;

namespace SagaPoc.Shared.Mensagens.Comandos;

/// <summary>
/// Comando para validar o pedido com o restaurante.
/// Verifica se o restaurante está aberto e se os itens estão disponíveis.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="RestauranteId">Identificador do restaurante.</param>
/// <param name="Itens">Lista de itens a serem validados.</param>
public record ValidarPedidoRestaurante(
    Guid CorrelacaoId,
    string RestauranteId,
    List<ItemPedido> Itens
);
