namespace SagaPoc.FluxoCaixa.Domain.Eventos;

public record LancamentoCreditoRegistrado(
    Guid LancamentoId,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante,
    string? Categoria
);
