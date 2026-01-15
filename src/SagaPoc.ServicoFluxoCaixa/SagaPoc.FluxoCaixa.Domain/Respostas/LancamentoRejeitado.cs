namespace SagaPoc.FluxoCaixa.Domain.Respostas;

public record LancamentoRejeitado(
    Guid CorrelationId,
    string CodigoErro,
    string MensagemErro
);
