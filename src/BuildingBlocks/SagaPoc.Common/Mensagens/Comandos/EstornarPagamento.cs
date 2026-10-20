namespace SagaPoc.Common.Mensagens.Comandos;

/// <summary>
/// Comando de compensação para estornar um pagamento.
/// Executado quando a SAGA falha após processar o pagamento.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="TransacaoId">Identificador da transação a ser estornada. Nulo quando a SAGA não recebeu a resposta do pagamento: o serviço estorna o que tiver cobrado para o pedido e recusa cobrança posterior.</param>
public record EstornarPagamento(
    Guid CorrelacaoId,
    string? TransacaoId
);
