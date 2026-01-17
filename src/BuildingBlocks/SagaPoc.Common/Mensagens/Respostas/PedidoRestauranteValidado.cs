namespace SagaPoc.Common.Mensagens.Respostas;

/// <summary>
/// Resposta da validação do pedido pelo restaurante.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Valido">Indica se o pedido é válido (restaurante aberto, itens disponíveis).</param>
/// <param name="ValorTotal">Valor total do pedido (calculado pelo restaurante).</param>
/// <param name="TempoPreparoMinutos">Tempo estimado de preparo em minutos.</param>
/// <param name="PedidoId">ID do pedido criado no sistema do restaurante (para compensação).</param>
/// <param name="MotivoRejeicao">Motivo da rejeição (se inválido).</param>
public record PedidoRestauranteValidado(
    Guid CorrelacaoId,
    bool Valido,
    decimal ValorTotal,
    int TempoPreparoMinutos,
    Guid? PedidoId,
    string? MotivoRejeicao
);
