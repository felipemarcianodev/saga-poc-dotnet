namespace SagaPoc.ServicoPagamento.Modelos;

/// <summary>
/// Dados de uma transação de pagamento processada.
/// </summary>
/// <param name="TransacaoId">Identificador único da transação.</param>
/// <param name="Autorizacao">Código de autorização do processador de pagamento.</param>
/// <param name="ValorProcessado">Valor que foi efetivamente processado.</param>
public record DadosTransacao(
    string TransacaoId,
    string Autorizacao,
    decimal ValorProcessado
);
