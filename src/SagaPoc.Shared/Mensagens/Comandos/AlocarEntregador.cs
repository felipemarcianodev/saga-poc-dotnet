namespace SagaPoc.Shared.Mensagens.Comandos;

/// <summary>
/// Comando para alocar um entregador para o pedido.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="RestauranteId">Identificador do restaurante (para buscar entregadores próximos).</param>
/// <param name="EnderecoEntrega">Endereço de entrega.</param>
/// <param name="TaxaEntrega">Taxa de entrega a ser paga ao entregador.</param>
public record AlocarEntregador(
    Guid CorrelacaoId,
    string RestauranteId,
    string EnderecoEntrega,
    decimal TaxaEntrega
);
