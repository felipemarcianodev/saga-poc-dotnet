namespace SagaPoc.Common.Mensagens.Respostas;

/// <summary>
/// Resposta indicando que um pagamento foi estornado.
/// Parte do processo de compensação da SAGA.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Sucesso">Indica se o estorno foi realizado com sucesso.</param>
/// <param name="TransacaoId">Identificador da transação estornada.</param>
public record PagamentoEstornado(
    Guid CorrelacaoId,
    bool Sucesso,
    string TransacaoId
);
