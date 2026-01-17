namespace SagaPoc.Common.Modelos;

/// <summary>
/// Tipos de notificação que podem ser enviadas ao cliente.
/// </summary>
public enum TipoNotificacao
{
    /// <summary>
    /// Pedido foi confirmado e está em processamento.
    /// </summary>
    PedidoConfirmado,

    /// <summary>
    /// Pedido foi cancelado.
    /// </summary>
    PedidoCancelado,

    /// <summary>
    /// Entregador foi alocado para o pedido.
    /// </summary>
    EntregadorAlocado,

    /// <summary>
    /// Pedido está em preparação no restaurante.
    /// </summary>
    PedidoEmPreparacao,

    /// <summary>
    /// Pedido saiu para entrega.
    /// </summary>
    PedidoSaiuParaEntrega,

    /// <summary>
    /// Pedido foi entregue ao cliente.
    /// </summary>
    PedidoEntregue
}
