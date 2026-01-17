using SagaPoc.Common.Modelos;

namespace SagaPoc.Common.Mensagens.Comandos;

/// <summary>
/// Comando para iniciar o processamento de um novo pedido.
/// Este comando inicia a SAGA de processamento de pedido.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação para rastrear a SAGA.</param>
/// <param name="ClienteId">Identificador do cliente que fez o pedido.</param>
/// <param name="RestauranteId">Identificador do restaurante.</param>
/// <param name="Itens">Lista de itens do pedido.</param>
/// <param name="EnderecoEntrega">Endereço para entrega.</param>
/// <param name="FormaPagamento">Forma de pagamento (ex: "cartao_credito", "pix").</param>
public record IniciarPedido(
    Guid CorrelacaoId,
    string ClienteId,
    string RestauranteId,
    List<ItemPedido> Itens,
    string EnderecoEntrega,
    string FormaPagamento
);
