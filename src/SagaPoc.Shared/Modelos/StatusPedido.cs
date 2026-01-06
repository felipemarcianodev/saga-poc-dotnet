namespace SagaPoc.Shared.Modelos;

/// <summary>
/// Status possíveis de um pedido.
/// </summary>
public enum StatusPedido
{
    /// <summary>
    /// Pedido foi criado e está aguardando processamento.
    /// </summary>
    Pendente,

    /// <summary>
    /// Pedido foi confirmado e validado.
    /// </summary>
    Confirmado,

    /// <summary>
    /// Pedido está sendo preparado pelo restaurante.
    /// </summary>
    EmPreparacao,

    /// <summary>
    /// Pedido saiu para entrega.
    /// </summary>
    SaiuParaEntrega,

    /// <summary>
    /// Pedido foi entregue ao cliente.
    /// </summary>
    Entregue,

    /// <summary>
    /// Pedido foi cancelado.
    /// </summary>
    Cancelado
}
