using System.ComponentModel.DataAnnotations;

namespace SagaPoc.Common.Modelos;

/// <summary>
/// Representa um item do pedido.
/// </summary>
/// <param name="ProdutoId">Identificador do produto.</param>
/// <param name="Nome">Nome do produto.</param>
/// <param name="Quantidade">Quantidade solicitada.</param>
/// <param name="PrecoUnitario">Preço unitário do produto.</param>
/// <remarks>
/// As anotações valem na entrada da API de pedidos (validação do modelo do ASP.NET Core).
/// Regras de negócio, como o limite de 10 unidades, ficam no serviço de restaurante.
/// </remarks>
public record ItemPedido(
    [Required(ErrorMessage = "ProdutoId é obrigatório")] string ProdutoId,
    [Required(ErrorMessage = "Nome do item é obrigatório")] string Nome,
    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero")] int Quantidade,
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "O preço unitário deve ser maior que zero")] decimal PrecoUnitario)
{
    /// <summary>
    /// Calcula o valor total do item (Quantidade * PrecoUnitario).
    /// </summary>
    public decimal ValorTotal => Quantidade * PrecoUnitario;
}
