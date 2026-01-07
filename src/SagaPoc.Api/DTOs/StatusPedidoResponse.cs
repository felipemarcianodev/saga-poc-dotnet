namespace SagaPoc.Api.DTOs;

/// <summary>
/// Response contendo o status atual de um pedido.
/// </summary>
public record StatusPedidoResponse
{
    /// <summary>
    /// ID do pedido.
    /// </summary>
    public Guid PedidoId { get; init; }

    /// <summary>
    /// Status atual do pedido.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Data/hora da última atualização.
    /// </summary>
    public DateTime UltimaAtualizacao { get; init; }

    /// <summary>
    /// Mensagem adicional sobre o status (opcional).
    /// </summary>
    public string? Mensagem { get; init; }

    /// <summary>
    /// Detalhes adicionais do pedido (se disponível).
    /// </summary>
    public DetalhesPedido? Detalhes { get; init; }
}

/// <summary>
/// Detalhes adicionais do pedido.
/// </summary>
public record DetalhesPedido
{
    /// <summary>
    /// ID do cliente.
    /// </summary>
    public string? ClienteId { get; init; }

    /// <summary>
    /// ID do restaurante.
    /// </summary>
    public string? RestauranteId { get; init; }

    /// <summary>
    /// Valor total do pedido.
    /// </summary>
    public decimal? ValorTotal { get; init; }

    /// <summary>
    /// Endereço de entrega.
    /// </summary>
    public string? EnderecoEntrega { get; init; }

    /// <summary>
    /// ID do entregador alocado (se disponível).
    /// </summary>
    public string? EntregadorId { get; init; }

    /// <summary>
    /// Tempo estimado de preparo em minutos.
    /// </summary>
    public int? TempoPreparoMinutos { get; init; }

    /// <summary>
    /// Tempo estimado de entrega em minutos.
    /// </summary>
    public int? TempoEntregaMinutos { get; init; }
}
