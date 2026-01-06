namespace SagaPoc.Shared.Modelos;

/// <summary>
/// Representa um item do pedido.
/// </summary>
/// <param name="ProdutoId">Identificador do produto.</param>
/// <param name="Nome">Nome do produto.</param>
/// <param name="Quantidade">Quantidade solicitada.</param>
/// <param name="PrecoUnitario">Preço unitário do produto.</param>
public record ItemPedido(
    string ProdutoId,
    string Nome,
    int Quantidade,
    decimal PrecoUnitario)
{
    /// <summary>
    /// Calcula o valor total do item (Quantidade * PrecoUnitario).
    /// </summary>
    public decimal ValorTotal => Quantidade * PrecoUnitario;
}
