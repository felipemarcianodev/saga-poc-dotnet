namespace SagaPoc.Shared.Mensagens.Respostas;

/// <summary>
/// Resposta indicando que um pedido foi cancelado no restaurante.
/// Parte do processo de compensação da SAGA.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Sucesso">Indica se o cancelamento foi realizado com sucesso.</param>
/// <param name="PedidoId">Identificador do pedido cancelado.</param>
public record PedidoRestauranteCancelado(
    Guid CorrelacaoId,
    bool Sucesso,
    Guid PedidoId
);
