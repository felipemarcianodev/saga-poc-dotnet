using SagaPoc.Common.Modelos;
using SagaPoc.Common.ResultPattern;
using SagaPoc.ServicoRestaurante.Modelos;

namespace SagaPoc.ServicoRestaurante.Servicos;

/// <summary>
/// Interface para o serviço de validação de pedidos de restaurante.
/// </summary>
public interface IServicoRestaurante
{
    /// <summary>
    /// Valida um pedido verificando se o restaurante está aberto e se os itens estão disponíveis.
    /// </summary>
    /// <param name="restauranteId">Identificador do restaurante.</param>
    /// <param name="itens">Lista de itens do pedido.</param>
    /// <returns>Resultado contendo dados da validação ou erro.</returns>
    Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens
    );

    /// <summary>
    /// Cancela um pedido previamente validado (operação de compensação).
    /// </summary>
    /// <param name="restauranteId">Identificador do restaurante.</param>
    /// <param name="pedidoId">Identificador do pedido a ser cancelado.</param>
    /// <returns>Resultado indicando sucesso ou falha do cancelamento.</returns>
    Task<Resultado<Unit>> CancelarPedidoAsync(
        string restauranteId,
        Guid pedidoId
    );
}
