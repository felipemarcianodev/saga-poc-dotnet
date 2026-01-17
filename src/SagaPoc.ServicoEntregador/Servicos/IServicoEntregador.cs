using SagaPoc.Common.ResultPattern;
using SagaPoc.ServicoEntregador.Modelos;

namespace SagaPoc.ServicoEntregador.Servicos;

/// <summary>
/// Interface para o serviço de alocação de entregadores.
/// </summary>
public interface IServicoEntregador
{
    /// <summary>
    /// Aloca um entregador disponível para realizar uma entrega.
    /// </summary>
    /// <param name="restauranteId">Identificador do restaurante (ponto de origem).</param>
    /// <param name="enderecoEntrega">Endereço de destino.</param>
    /// <param name="taxaEntrega">Taxa de entrega oferecida.</param>
    /// <returns>Resultado contendo dados da alocação ou erro.</returns>
    Task<Resultado<DadosAlocacao>> AlocarAsync(
        string restauranteId,
        string enderecoEntrega,
        decimal taxaEntrega
    );

    /// <summary>
    /// Libera um entregador previamente alocado (operação de compensação).
    /// </summary>
    /// <param name="entregadorId">Identificador do entregador a ser liberado.</param>
    /// <returns>Resultado indicando sucesso ou falha da liberação.</returns>
    Task<Resultado<Unit>> LiberarAsync(string entregadorId);
}
