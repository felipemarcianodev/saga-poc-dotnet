using SagaPoc.Common.Modelos;
using System.ComponentModel.DataAnnotations;

namespace SagaPoc.Api.DTOs;

/// <summary>
/// Request para criação de um novo pedido.
/// </summary>
public record CriarPedidoRequest
{
    /// <summary>
    /// Identificador do cliente que está fazendo o pedido.
    /// </summary>
    [Required(ErrorMessage = "ClienteId é obrigatório")]
    public string ClienteId { get; init; } = string.Empty;

    /// <summary>
    /// Identificador do restaurante.
    /// </summary>
    [Required(ErrorMessage = "RestauranteId é obrigatório")]
    public string RestauranteId { get; init; } = string.Empty;

    /// <summary>
    /// Lista de itens do pedido.
    /// </summary>
    [Required(ErrorMessage = "Itens são obrigatórios")]
    [MinLength(1, ErrorMessage = "O pedido deve ter pelo menos 1 item")]
    public List<ItemPedido> Itens { get; init; } = new();

    /// <summary>
    /// Endereço completo para entrega.
    /// </summary>
    [Required(ErrorMessage = "EnderecoEntrega é obrigatório")]
    public string EnderecoEntrega { get; init; } = string.Empty;

    /// <summary>
    /// Forma de pagamento (ex: "cartao_credito", "pix", "dinheiro").
    /// </summary>
    [Required(ErrorMessage = "FormaPagamento é obrigatória")]
    public string FormaPagamento { get; init; } = string.Empty;
}
