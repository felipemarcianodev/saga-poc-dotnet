namespace SagaPoc.Common.Mensagens.Comandos;

/// <summary>
/// Comando de compensação para estornar um pagamento.
/// Executado quando a SAGA falha após processar o pagamento.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="TransacaoId">Identificador da transação a ser estornada.</param>
public record EstornarPagamento(
    Guid CorrelacaoId,
    string TransacaoId
);
