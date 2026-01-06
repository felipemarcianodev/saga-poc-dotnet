namespace SagaPoc.Shared.Mensagens.Respostas;

/// <summary>
/// Resposta do processamento de pagamento.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Sucesso">Indica se o pagamento foi processado com sucesso.</param>
/// <param name="TransacaoId">Identificador da transação (se sucesso).</param>
/// <param name="MotivoFalha">Motivo da falha (se não processado).</param>
public record PagamentoProcessado(
    Guid CorrelacaoId,
    bool Sucesso,
    string? TransacaoId,
    string? MotivoFalha
);
