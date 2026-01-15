namespace SagaPoc.FluxoCaixa.Domain.Eventos;

public record LancamentoCancelado(
    Guid LancamentoId,
    string Motivo,
    DateTime DataCancelamento
);
