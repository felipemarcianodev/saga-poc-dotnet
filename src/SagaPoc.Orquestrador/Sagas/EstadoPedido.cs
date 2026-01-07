using MassTransit;

namespace SagaPoc.Orquestrador.Sagas;

/// <summary>
/// Representa o estado de um pedido durante a execução da SAGA.
/// Implementa SagaStateMachineInstance para persistência do estado.
/// </summary>
public class EstadoPedido : SagaStateMachineInstance
{
    /// <summary>
    /// ID de correlação único para rastreamento da SAGA.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Estado atual da máquina de estados.
    /// </summary>
    public string EstadoAtual { get; set; } = string.Empty;

    // ==================== Dados do Pedido ====================

    /// <summary>
    /// ID do cliente que fez o pedido.
    /// </summary>
    public string ClienteId { get; set; } = string.Empty;

    /// <summary>
    /// ID do restaurante onde o pedido foi feito.
    /// </summary>
    public string RestauranteId { get; set; } = string.Empty;

    /// <summary>
    /// Valor total do pedido.
    /// </summary>
    public decimal ValorTotal { get; set; }

    /// <summary>
    /// Endereço para entrega do pedido.
    /// </summary>
    public string EnderecoEntrega { get; set; } = string.Empty;

    /// <summary>
    /// Forma de pagamento utilizada.
    /// </summary>
    public string FormaPagamento { get; set; } = string.Empty;

    // ==================== Controle de Compensação ====================

    /// <summary>
    /// ID da transação de pagamento (necessário para estorno).
    /// </summary>
    public string? TransacaoId { get; set; }

    /// <summary>
    /// ID do entregador alocado (necessário para liberação).
    /// </summary>
    public string? EntregadorId { get; set; }

    /// <summary>
    /// ID do pedido no sistema do restaurante (necessário para cancelamento).
    /// </summary>
    public Guid? PedidoRestauranteId { get; set; }

    // ==================== Timestamps ====================

    /// <summary>
    /// Data e hora de início do processamento da SAGA.
    /// </summary>
    public DateTime DataInicio { get; set; }

    /// <summary>
    /// Data e hora de conclusão da SAGA (sucesso ou cancelamento).
    /// </summary>
    public DateTime? DataConclusao { get; set; }

    // ==================== Métricas ====================

    /// <summary>
    /// Tempo estimado de preparo do pedido em minutos.
    /// </summary>
    public int TempoPreparoMinutos { get; set; }

    /// <summary>
    /// Tempo estimado de entrega em minutos.
    /// </summary>
    public int TempoEntregaMinutos { get; set; }

    /// <summary>
    /// Taxa de entrega cobrada.
    /// </summary>
    public decimal TaxaEntrega { get; set; }

    // ==================== Controle de Erros ====================

    /// <summary>
    /// Mensagem de erro em caso de falha.
    /// </summary>
    public string? MensagemErro { get; set; }

    /// <summary>
    /// Motivo da rejeição/cancelamento do pedido.
    /// </summary>
    public string? MotivoRejeicao { get; set; }
}
