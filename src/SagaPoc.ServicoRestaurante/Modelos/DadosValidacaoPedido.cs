namespace SagaPoc.ServicoRestaurante.Modelos;

/// <summary>
/// Dados retornados após validação bem-sucedida de um pedido no restaurante.
/// </summary>
/// <param name="ValorTotal">Valor total do pedido calculado.</param>
/// <param name="TempoPreparoMinutos">Tempo estimado de preparo em minutos.</param>
/// <param name="PedidoId">Identificador do pedido criado no sistema do restaurante.</param>
public record DadosValidacaoPedido(
    decimal ValorTotal,
    int TempoPreparoMinutos,
    Guid PedidoId
);
