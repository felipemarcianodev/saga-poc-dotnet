namespace SagaPoc.Api.DTOs;

/// <summary>
/// Response retornada após a criação de um pedido.
/// </summary>
public record PedidoCriadoResponse
{
    /// <summary>
    /// ID de correlação do pedido (usado para rastreamento).
    /// </summary>
    public Guid PedidoId { get; init; }

    /// <summary>
    /// Mensagem informativa sobre o pedido.
    /// </summary>
    public string Mensagem { get; init; } = string.Empty;

    /// <summary>
    /// Status atual do pedido.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Data/hora em que o pedido foi recebido.
    /// </summary>
    public DateTime DataRecebimento { get; init; }
}
