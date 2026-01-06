namespace SagaPoc.Shared.Mensagens.Comandos;

/// <summary>
/// Comando para processar o pagamento do pedido.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="ClienteId">Identificador do cliente.</param>
/// <param name="ValorTotal">Valor total a ser cobrado.</param>
/// <param name="FormaPagamento">Forma de pagamento.</param>
public record ProcessarPagamento(
    Guid CorrelacaoId,
    string ClienteId,
    decimal ValorTotal,
    string FormaPagamento
);
