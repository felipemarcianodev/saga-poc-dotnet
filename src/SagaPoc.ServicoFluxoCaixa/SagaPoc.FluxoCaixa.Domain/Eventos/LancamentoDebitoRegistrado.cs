namespace SagaPoc.FluxoCaixa.Domain.Eventos;

public record LancamentoDebitoRegistrado(
    Guid LancamentoId,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante,
    string? Categoria
);
