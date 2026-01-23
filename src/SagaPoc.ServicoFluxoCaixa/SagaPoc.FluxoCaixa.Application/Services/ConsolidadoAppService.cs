using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SagaPoc.Common.ResultPattern;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Application.Services;

public class ConsolidadoAppService : IConsolidadoAppService
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly ICacheService _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ConsolidadoAppService> _logger;

    private static readonly TimeSpan MemoryCacheTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RedisCacheTtl = TimeSpan.FromMinutes(5);

    public ConsolidadoAppService(
        IConsolidadoDiarioRepository repository,
        ICacheService cache,
        IMemoryCache memoryCache,
        ILogger<ConsolidadoAppService> logger)
    {
        _repository = repository;
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<Resultado<ConsolidadoDiario>> ObterPorDataAsync(
        string comerciante,
        DateTime data,
        CancellationToken ct = default)
    {
        var cacheKey = $"consolidado:{comerciante}:{data:yyyy-MM-dd}";

        // Camada 1: Memory Cache
        if (_memoryCache.TryGetValue(cacheKey, out ConsolidadoDiario? cached) && cached is not null)
        {
            _logger.LogDebug("Consolidado retornado do memory cache: {Key}", cacheKey);
            return Resultado<ConsolidadoDiario>.Sucesso(cached);
        }

        // Camada 2: Redis Cache
        var cachedConsolidado = await _cache.GetAsync<ConsolidadoDiario>(cacheKey, ct);
        if (cachedConsolidado is not null)
        {
            _memoryCache.Set(cacheKey, cachedConsolidado, MemoryCacheTtl);
            _logger.LogDebug("Consolidado retornado do Redis: {Key}", cacheKey);
            return Resultado<ConsolidadoDiario>.Sucesso(cachedConsolidado);
        }

        // Camada 3: Banco de dados
        var resultado = await _repository.ObterPorDataAsync(data.Date, comerciante, ct);

        if (resultado.EhFalha)
        {
            _logger.LogWarning(
                "Consolidado nao encontrado: {Comerciante} - {Data}",
                comerciante, data);
            return resultado;
        }

        var consolidado = resultado.Valor;

        // Armazenar em ambos os caches
        await _cache.SetAsync(cacheKey, consolidado, RedisCacheTtl, ct);
        _memoryCache.Set(cacheKey, consolidado, MemoryCacheTtl);

        _logger.LogDebug("Consolidado retornado do banco e armazenado em cache: {Key}", cacheKey);

        return Resultado<ConsolidadoDiario>.Sucesso(consolidado);
    }

    public async Task<Resultado<IEnumerable<ConsolidadoDiario>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Buscando consolidados: {Comerciante} de {Inicio} a {Fim}",
            comerciante, inicio, fim);

        return await _repository.ObterPorPeriodoAsync(comerciante, inicio.Date, fim.Date, ct);
    }
}
