using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SagaPoc.FluxoCaixa.Api.DTOs;
using SagaPoc.FluxoCaixa.Consolidado.Servicos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsolidadoController : ControllerBase
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly ICacheService _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ConsolidadoController> _logger;

    public ConsolidadoController(
        IConsolidadoDiarioRepository repository,
        ICacheService cache,
        IMemoryCache memoryCache,
        ILogger<ConsolidadoController> logger)
    {
        _repository = repository;
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    /// <summary>
    /// Retorna o consolidado diário para uma data específica
    /// </summary>
    /// <remarks>
    /// **Performance**: Este endpoint é otimizado com cache em 3 camadas:
    /// 1. Memory Cache (in-process) - 1 minuto
    /// 2. Redis Cache (distributed) - 5 minutos
    /// 3. PostgreSQL (source of truth)
    ///
    /// Capacidade: 50 req/s com latência &lt; 50ms (P95)
    /// </remarks>
    [HttpGet("{comerciante}/{data:datetime}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "comerciante", "data" })]
    public async Task<IActionResult> ObterPorData(
        string comerciante,
        DateTime data,
        CancellationToken ct)
    {
        var cacheKey = $"consolidado:{comerciante}:{data:yyyy-MM-dd}";

        // Camada 1: Memory Cache (mais rápido)
        if (_memoryCache.TryGetValue(cacheKey, out ConsolidadoResponse? cachedResponse))
        {
            _logger.LogDebug("Consolidado retornado do memory cache: {Key}", cacheKey);
            return Ok(cachedResponse);
        }

        // Camada 2: Redis Cache
        var cachedConsolidado = await _cache.GetAsync<ConsolidadoResponse>(cacheKey, ct);
        if (cachedConsolidado is not null)
        {
            // Armazenar em memory cache para próximas requisições
            _memoryCache.Set(cacheKey, cachedConsolidado, TimeSpan.FromMinutes(1));

            _logger.LogDebug("Consolidado retornado do Redis: {Key}", cacheKey);
            return Ok(cachedConsolidado);
        }

        // Camada 3: Banco de dados
        var resultado = await _repository.ObterPorDataAsync(data.Date, comerciante, ct);

        if (resultado.EhFalha)
        {
            _logger.LogWarning(
                "Consolidado não encontrado: {Comerciante} - {Data}",
                comerciante,
                data);

            return NotFound(new { erro = resultado.Erro.Mensagem });
        }

        var consolidado = resultado.Valor;

        var response = new ConsolidadoResponse
        {
            Data = consolidado.Data,
            Comerciante = consolidado.Comerciante,
            TotalCreditos = consolidado.TotalCreditos,
            TotalDebitos = consolidado.TotalDebitos,
            SaldoDiario = consolidado.SaldoDiario,
            QuantidadeCreditos = consolidado.QuantidadeCreditos,
            QuantidadeDebitos = consolidado.QuantidadeDebitos,
            QuantidadeTotalLancamentos = consolidado.QuantidadeTotalLancamentos,
            UltimaAtualizacao = consolidado.UltimaAtualizacao
        };

        // Armazenar em ambos os caches
        await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5), ct);
        _memoryCache.Set(cacheKey, response, TimeSpan.FromMinutes(1));

        _logger.LogInformation(
            "Consolidado retornado do banco e armazenado em cache: {Key}",
            cacheKey);

        return Ok(response);
    }

    /// <summary>
    /// Retorna os consolidados de um período
    /// </summary>
    [HttpGet("{comerciante}/periodo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorPeriodo(
        string comerciante,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct)
    {
        var resultado = await _repository.ObterPorPeriodoAsync(
            comerciante,
            inicio.Date,
            fim.Date,
            ct);

        if (resultado.EhFalha)
            return BadRequest(new { erro = resultado.Erro.Mensagem });

        var consolidados = resultado.Valor.Select(c => new ConsolidadoResponse
        {
            Data = c.Data,
            Comerciante = c.Comerciante,
            TotalCreditos = c.TotalCreditos,
            TotalDebitos = c.TotalDebitos,
            SaldoDiario = c.SaldoDiario,
            QuantidadeCreditos = c.QuantidadeCreditos,
            QuantidadeDebitos = c.QuantidadeDebitos,
            QuantidadeTotalLancamentos = c.QuantidadeTotalLancamentos,
            UltimaAtualizacao = c.UltimaAtualizacao
        });

        return Ok(consolidados);
    }
}
