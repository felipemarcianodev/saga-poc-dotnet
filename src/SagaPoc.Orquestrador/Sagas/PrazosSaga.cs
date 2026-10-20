namespace SagaPoc.Orquestrador.Sagas;

/// <summary>
/// Mensagem que a SAGA agenda para si mesma ao começar: se chegar com o pedido ainda em andamento,
/// a SAGA compensa o que já foi feito. Pedido já concluído não tem mais SAGA, e a mensagem é ignorada.
/// </summary>
public record PrazoPedidoExpirado(Guid CorrelacaoId);

/// <summary>
/// Mensagem que a SAGA agenda para si mesma ao iniciar a compensação: se chegar com passos ainda
/// sem resposta, eles contam como falha e a SAGA termina como compensação falha.
/// </summary>
public record PrazoCompensacaoExpirado(Guid CorrelacaoId);
