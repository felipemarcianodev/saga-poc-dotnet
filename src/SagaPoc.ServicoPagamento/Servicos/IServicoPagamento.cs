using SagaPoc.Shared.ResultPattern;
using SagaPoc.ServicoPagamento.Modelos;

namespace SagaPoc.ServicoPagamento.Servicos;

/// <summary>
/// Interface para o serviço de processamento de pagamentos.
/// </summary>
public interface IServicoPagamento
{
    /// <summary>
    /// Processa um pagamento para um cliente.
    /// </summary>
    /// <param name="clienteId">Identificador do cliente.</param>
    /// <param name="valorTotal">Valor total a ser cobrado.</param>
    /// <param name="formaPagamento">Forma de pagamento (Cartão, PIX, etc).</param>
    /// <returns>Resultado contendo dados da transação ou erro.</returns>
    Task<Resultado<DadosTransacao>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento
    );

    /// <summary>
    /// Estorna um pagamento previamente processado (operação de compensação).
    /// </summary>
    /// <param name="transacaoId">Identificador da transação a ser estornada.</param>
    /// <returns>Resultado indicando sucesso ou falha do estorno.</returns>
    Task<Resultado<Unit>> EstornarAsync(string transacaoId);
}
