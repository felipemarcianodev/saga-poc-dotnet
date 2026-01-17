using SagaPoc.Common.ResultPattern;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Domain.Repositorios;

public interface IConsolidadoDiarioRepository
{
    Task<Resultado<ConsolidadoDiario>> ObterOuCriarAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default);

    Task<Resultado<ConsolidadoDiario>> ObterPorDataAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default);

    Task<Resultado<IEnumerable<ConsolidadoDiario>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);

    Task<Resultado<Unit>> SalvarAsync(
        ConsolidadoDiario consolidado,
        CancellationToken ct = default);
}
