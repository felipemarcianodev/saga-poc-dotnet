using SagaPoc.Common.ResultPattern;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Application.Services;

public interface ILancamentoAppService
{
    Task<Resultado<Lancamento>> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Resultado<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);
}
