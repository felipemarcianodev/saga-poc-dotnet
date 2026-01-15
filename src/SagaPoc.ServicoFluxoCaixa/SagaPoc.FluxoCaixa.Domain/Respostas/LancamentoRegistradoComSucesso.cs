using SagaPoc.FluxoCaixa.Domain.ValueObjects;

namespace SagaPoc.FluxoCaixa.Domain.Respostas;

public record LancamentoRegistradoComSucesso(
    Guid CorrelationId,
    Guid LancamentoId,
    EnumTipoLancamento Tipo,
    decimal Valor,
    DateTime DataLancamento
);
