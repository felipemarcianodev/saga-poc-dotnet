using Microsoft.Extensions.Logging;
using SagaPoc.Common.ResultPattern;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Application.Services;

public class LancamentoAppService : ILancamentoAppService
{
    private readonly ILancamentoRepository _repository;
    private readonly ILogger<LancamentoAppService> _logger;

    public LancamentoAppService(
        ILancamentoRepository repository,
        ILogger<LancamentoAppService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Resultado<Lancamento>> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Buscando lancamento por ID: {Id}", id);
        return await _repository.ObterPorIdAsync(id, ct);
    }

    public async Task<Resultado<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Buscando lancamentos: {Comerciante} de {Inicio} a {Fim}",
            comerciante, inicio, fim);

        return await _repository.ObterPorPeriodoAsync(comerciante, inicio, fim, ct);
    }
}
