using SagaPoc.FluxoCaixa.Domain.ValueObjects;

namespace SagaPoc.FluxoCaixa.Domain.Comandos;

public record RegistrarLancamento(
    Guid CorrelationId,
    EnumTipoLancamento Tipo,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante,
    string? Categoria
);
