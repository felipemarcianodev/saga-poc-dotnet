using SagaPoc.Common.ResultPattern;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Application.Services;

public interface IConsolidadoAppService
{
    Task<Resultado<ConsolidadoDiario>> ObterPorDataAsync(
        string comerciante,
        DateTime data,
        CancellationToken ct = default);

    Task<Resultado<IEnumerable<ConsolidadoDiario>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);
}
