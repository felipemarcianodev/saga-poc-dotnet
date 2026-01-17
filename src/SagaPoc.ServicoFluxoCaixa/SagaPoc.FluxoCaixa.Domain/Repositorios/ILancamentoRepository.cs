using SagaPoc.Common.ResultPattern;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Domain.Repositorios;

public interface ILancamentoRepository
{
    Task<Resultado<Lancamento>> AdicionarAsync(Lancamento lancamento, CancellationToken ct = default);
    Task<Resultado<Lancamento>> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Resultado<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);
    Task<Resultado<IEnumerable<Lancamento>>> ObterPorDataAsync(
        string comerciante,
        DateTime data,
        CancellationToken ct = default);
    Task<Resultado<Unit>> AtualizarAsync(Lancamento lancamento, CancellationToken ct = default);
}
