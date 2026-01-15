# FASE 25: Implementação do Serviço de Consolidado Diário (Read Model)

## Objetivos

- Implementar o serviço de relatório de consolidado diário
- Garantir performance para 50 requisições/segundo (NFR)
- Aplicar estratégias de cache (Redis + Memory Cache)
- Implementar processamento assíncrono de eventos
- Garantir resiliência independente do serviço de lançamentos
- Otimizar consultas para leitura

## Entregas

### 1. **Agregado ConsolidadoDiario**

```csharp
// src/SagaPoc.FluxoCaixa.Domain/Agregados/ConsolidadoDiario.cs

namespace SagaPoc.FluxoCaixa.Domain.Agregados;

public class ConsolidadoDiario : AggregateRoot
{
    public Guid Id { get; private set; }
    public DateTime Data { get; private set; }
    public string Comerciante { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal SaldoDiario => TotalCreditos - TotalDebitos;
    public int QuantidadeCreditos { get; private set; }
    public int QuantidadeDebitos { get; private set; }
    public int QuantidadeTotalLancamentos => QuantidadeCreditos + QuantidadeDebitos;
    public DateTime UltimaAtualizacao { get; private set; }

    // Para EF Core
    private ConsolidadoDiario() { }

    public static ConsolidadoDiario Criar(DateTime data, string comerciante)
    {
        return new ConsolidadoDiario
        {
            Id = Guid.NewGuid(),
            Data = data.Date,
            Comerciante = comerciante,
            TotalCreditos = 0,
            TotalDebitos = 0,
            QuantidadeCreditos = 0,
            QuantidadeDebitos = 0,
            UltimaAtualizacao = DateTime.UtcNow
        };
    }

    public void AplicarCredito(decimal valor)
    {
        if (valor <= 0)
            throw new InvalidOperationException("Valor de crédito deve ser positivo");

        TotalCreditos += valor;
        QuantidadeCreditos++;
        UltimaAtualizacao = DateTime.UtcNow;
    }

    public void AplicarDebito(decimal valor)
    {
        if (valor <= 0)
            throw new InvalidOperationException("Valor de débito deve ser positivo");

        TotalDebitos += valor;
        QuantidadeDebitos++;
        UltimaAtualizacao = DateTime.UtcNow;
    }

    public void Recalcular(IEnumerable<Lancamento> lancamentos)
    {
        TotalCreditos = 0;
        TotalDebitos = 0;
        QuantidadeCreditos = 0;
        QuantidadeDebitos = 0;

        foreach (var lancamento in lancamentos)
        {
            if (lancamento.Tipo == EnumTipoLancamento.Credito)
                AplicarCredito(lancamento.Valor);
            else
                AplicarDebito(lancamento.Valor);
        }

        UltimaAtualizacao = DateTime.UtcNow;
    }
}
```

### 2. **Repositório de Consolidado**

```csharp
// src/SagaPoc.FluxoCaixa.Domain/Repositorios/IConsolidadoDiarioRepository.cs

using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Domain.Repositorios;

public interface IConsolidadoDiarioRepository
{
    Task<Result<ConsolidadoDiario>> ObterOuCriarAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default);

    Task<Result<ConsolidadoDiario>> ObterPorDataAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default);

    Task<Result<IEnumerable<ConsolidadoDiario>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);

    Task<Result> SalvarAsync(
        ConsolidadoDiario consolidado,
        CancellationToken ct = default);
}
```

#### Implementação com EF Core

```csharp
// src/SagaPoc.FluxoCaixa.Infrastructure/Repositorios/ConsolidadoDiarioRepository.cs

using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Infrastructure.Repositorios;

public class ConsolidadoDiarioRepository : IConsolidadoDiarioRepository
{
    private readonly ConsolidadoDbContext _context;
    private readonly ILogger<ConsolidadoDiarioRepository> _logger;

    public ConsolidadoDiarioRepository(
        ConsolidadoDbContext context,
        ILogger<ConsolidadoDiarioRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<ConsolidadoDiario>> ObterOuCriarAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default)
    {
        try
        {
            var consolidado = await _context.Consolidados
                .FirstOrDefaultAsync(
                    c => c.Data == data.Date && c.Comerciante == comerciante,
                    ct);

            if (consolidado is null)
            {
                consolidado = ConsolidadoDiario.Criar(data.Date, comerciante);
                await _context.Consolidados.AddAsync(consolidado, ct);
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Consolidado criado para {Data} - Comerciante: {Comerciante}",
                    data.Date,
                    comerciante);
            }

            return Result<ConsolidadoDiario>.Success(consolidado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ou criar consolidado");
            return Result<ConsolidadoDiario>.Failure(new Erro(
                "Consolidado.ErroObtencao",
                "Erro ao obter consolidado diário"));
        }
    }

    public async Task<Result<ConsolidadoDiario>> ObterPorDataAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default)
    {
        try
        {
            var consolidado = await _context.Consolidados
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.Data == data.Date && c.Comerciante == comerciante,
                    ct);

            if (consolidado is null)
            {
                return Result<ConsolidadoDiario>.Failure(new Erro(
                    "Consolidado.NaoEncontrado",
                    $"Consolidado não encontrado para {data:yyyy-MM-dd}"));
            }

            return Result<ConsolidadoDiario>.Success(consolidado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar consolidado por data");
            return Result<ConsolidadoDiario>.Failure(new Erro(
                "Consolidado.ErroBusca",
                "Erro ao buscar consolidado"));
        }
    }

    public async Task<Result<IEnumerable<ConsolidadoDiario>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default)
    {
        try
        {
            var consolidados = await _context.Consolidados
                .AsNoTracking()
                .Where(c => c.Comerciante == comerciante
                         && c.Data >= inicio.Date
                         && c.Data <= fim.Date)
                .OrderBy(c => c.Data)
                .ToListAsync(ct);

            return Result<IEnumerable<ConsolidadoDiario>>.Success(consolidados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar consolidados por período");
            return Result<IEnumerable<ConsolidadoDiario>>.Failure(new Erro(
                "Consolidado.ErroBusca",
                "Erro ao buscar consolidados por período"));
        }
    }

    public async Task<Result> SalvarAsync(
        ConsolidadoDiario consolidado,
        CancellationToken ct = default)
    {
        try
        {
            _context.Consolidados.Update(consolidado);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Consolidado atualizado: {Data} - Saldo: {Saldo}",
                consolidado.Data,
                consolidado.SaldoDiario);

            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro ao salvar consolidado");
            return Result.Failure(new Erro(
                "Consolidado.ErroSalvar",
                "Erro ao salvar consolidado"));
        }
    }
}
```

### 3. **Handlers de Eventos**

```csharp
// src/SagaPoc.FluxoCaixa.Consolidado/Handlers/LancamentoCreditoRegistradoHandler.cs

using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Consolidado.Handlers;

public class LancamentoCreditoRegistradoHandler
    : IHandleMessages<LancamentoCreditoRegistrado>
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<LancamentoCreditoRegistradoHandler> _logger;

    public LancamentoCreditoRegistradoHandler(
        IConsolidadoDiarioRepository repository,
        ICacheService cache,
        ILogger<LancamentoCreditoRegistradoHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(LancamentoCreditoRegistrado evento)
    {
        _logger.LogInformation(
            "Processando crédito: {LancamentoId} - Valor: {Valor} - Data: {Data}",
            evento.LancamentoId,
            evento.Valor,
            evento.DataLancamento);

        // Obter ou criar consolidado
        var resultadoConsolidado = await _repository.ObterOuCriarAsync(
            evento.DataLancamento,
            evento.Comerciante);

        if (resultadoConsolidado.IsFailure)
        {
            _logger.LogError(
                "Falha ao obter consolidado: {Erro}",
                resultadoConsolidado.Error.Mensagem);
            throw new InvalidOperationException(resultadoConsolidado.Error.Mensagem);
        }

        var consolidado = resultadoConsolidado.Value;

        // Aplicar crédito
        consolidado.AplicarCredito(evento.Valor);

        // Salvar
        var resultadoSalvar = await _repository.SalvarAsync(consolidado);

        if (resultadoSalvar.IsFailure)
        {
            _logger.LogError("Falha ao salvar consolidado: {Erro}",
                resultadoSalvar.Error.Mensagem);
            throw new InvalidOperationException(resultadoSalvar.Error.Mensagem);
        }

        // Invalidar cache
        await _cache.RemoveAsync(
            $"consolidado:{evento.Comerciante}:{evento.DataLancamento:yyyy-MM-dd}");

        _logger.LogInformation(
            "Consolidado atualizado com crédito. Saldo atual: {Saldo}",
            consolidado.SaldoDiario);
    }
}
```

```csharp
// src/SagaPoc.FluxoCaixa.Consolidado/Handlers/LancamentoDebitoRegistradoHandler.cs

using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Consolidado.Handlers;

public class LancamentoDebitoRegistradoHandler
    : IHandleMessages<LancamentoDebitoRegistrado>
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<LancamentoDebitoRegistradoHandler> _logger;

    public LancamentoDebitoRegistradoHandler(
        IConsolidadoDiarioRepository repository,
        ICacheService cache,
        ILogger<LancamentoDebitoRegistradoHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(LancamentoDebitoRegistrado evento)
    {
        _logger.LogInformation(
            "Processando débito: {LancamentoId} - Valor: {Valor} - Data: {Data}",
            evento.LancamentoId,
            evento.Valor,
            evento.DataLancamento);

        // Obter ou criar consolidado
        var resultadoConsolidado = await _repository.ObterOuCriarAsync(
            evento.DataLancamento,
            evento.Comerciante);

        if (resultadoConsolidado.IsFailure)
        {
            _logger.LogError(
                "Falha ao obter consolidado: {Erro}",
                resultadoConsolidado.Error.Mensagem);
            throw new InvalidOperationException(resultadoConsolidado.Error.Mensagem);
        }

        var consolidado = resultadoConsolidado.Value;

        // Aplicar débito
        consolidado.AplicarDebito(evento.Valor);

        // Salvar
        var resultadoSalvar = await _repository.SalvarAsync(consolidado);

        if (resultadoSalvar.IsFailure)
        {
            _logger.LogError("Falha ao salvar consolidado: {Erro}",
                resultadoSalvar.Error.Mensagem);
            throw new InvalidOperationException(resultadoSalvar.Error.Mensagem);
        }

        // Invalidar cache
        await _cache.RemoveAsync(
            $"consolidado:{evento.Comerciante}:{evento.DataLancamento:yyyy-MM-dd}");

        _logger.LogInformation(
            "Consolidado atualizado com débito. Saldo atual: {Saldo}",
            consolidado.SaldoDiario);
    }
}
```

### 4. **Cache Service (Redis + Memory Cache)**

```csharp
// src/SagaPoc.FluxoCaixa.Consolidado/Servicos/ICacheService.cs

namespace SagaPoc.FluxoCaixa.Consolidado.Servicos;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

```csharp
// src/SagaPoc.FluxoCaixa.Consolidado/Servicos/RedisCacheService.cs

using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace SagaPoc.FluxoCaixa.Consolidado.Servicos;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IDistributedCache cache,
        ILogger<RedisCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var json = await _cache.GetStringAsync(key, ct);

            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao buscar do cache: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5)
            };

            await _cache.SetStringAsync(key, json, options, ct);

            _logger.LogDebug("Item adicionado ao cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao adicionar no cache: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
            _logger.LogDebug("Item removido do cache: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao remover do cache: {Key}", key);
        }
    }
}
```

### 5. **API - Controller de Consolidado**

```csharp
// src/SagaPoc.FluxoCaixa.Api/Controllers/ConsolidadoController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SagaPoc.FluxoCaixa.Api.DTOs;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Consolidado.Servicos;

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

        if (resultado.IsFailure)
        {
            _logger.LogWarning(
                "Consolidado não encontrado: {Comerciante} - {Data}",
                comerciante,
                data);

            return NotFound(new { erro = resultado.Error.Mensagem });
        }

        var consolidado = resultado.Value;

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

        if (resultado.IsFailure)
            return BadRequest(new { erro = resultado.Error.Mensagem });

        var consolidados = resultado.Value.Select(c => new ConsolidadoResponse
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
```

### 6. **DTOs**

```csharp
// src/SagaPoc.FluxoCaixa.Api/DTOs/ConsolidadoResponse.cs

namespace SagaPoc.FluxoCaixa.Api.DTOs;

public class ConsolidadoResponse
{
    public DateTime Data { get; set; }
    public string Comerciante { get; set; } = string.Empty;
    public decimal TotalCreditos { get; set; }
    public decimal TotalDebitos { get; set; }
    public decimal SaldoDiario { get; set; }
    public int QuantidadeCreditos { get; set; }
    public int QuantidadeDebitos { get; set; }
    public int QuantidadeTotalLancamentos { get; set; }
    public DateTime UltimaAtualizacao { get; set; }
}
```

### 7. **Configuração - Program.cs**

```csharp
// src/SagaPoc.FluxoCaixa.Api/Program.cs

var builder = WebApplication.CreateBuilder(args);

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "FluxoCaixa:";
});

// Memory Cache
builder.Services.AddMemoryCache();

// Cache Service
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// Response Caching
builder.Services.AddResponseCaching();

// Rate Limiting (NFR: 50 req/s)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("consolidado", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromSeconds(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

var app = builder.Build();

app.UseResponseCaching();
app.UseRateLimiter();

app.MapControllers().RequireRateLimiting("consolidado");

app.Run();
```

### 8. **Docker Compose - Redis**

```yaml
# docker/docker-compose.yml (adicionar)

services:
  redis:
    image: redis:7-alpine
    container_name: fluxocaixa-redis
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    networks:
      - saga-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
    restart: unless-stopped

volumes:
  redis_data:
```

### 9. **appsettings.json**

```json
{
  "ConnectionStrings": {
    "ConsolidadoDb": "Host=localhost;Port=5434;Database=fluxocaixa_consolidado;Username=saga;Password=saga123",
    "Redis": "localhost:6379,abortConnect=false"
  },
  "Cache": {
    "MemoryCacheDurationMinutes": 1,
    "RedisCacheDurationMinutes": 5,
    "ResponseCacheDurationSeconds": 60
  },
  "RateLimiting": {
    "RequestsPerSecond": 50,
    "QueueLimit": 10
  }
}
```

## Critérios de Aceitação

- [ ] Agregado `ConsolidadoDiario` implementado
- [ ] Handlers de eventos (crédito/débito) implementados
- [ ] Repository com EF Core configurado
- [ ] Cache em 3 camadas (Memory + Redis + Response Cache)
- [ ] Rate limiting configurado para 50 req/s
- [ ] API com endpoint de consulta otimizado
- [ ] Resiliência independente do serviço de lançamentos
- [ ] Latência P95 < 50ms (com cache)
- [ ] Logs estruturados
- [ ] Health checks configurados

## NFRs Atendidos

| NFR | Implementação | Status |
|-----|---------------|--------|
| 50 req/s | Rate Limiter + Cache | |
| < 5% perda | Queue com backpressure | |
| Disponibilidade independente | Comunicação assíncrona via eventos | |
| Performance | Cache em 3 camadas | |

## Trade-offs

**Benefícios:**
- Alta performance (cache agressivo)
- Escalabilidade horizontal
- Resiliência (eventual consistency)
- Custo otimizado (menos queries ao banco)

**Considerações:**
- Eventual consistency (dados podem estar desatualizados por até 5 min)
- Complexidade de invalidação de cache
- Necessidade de Redis em produção

## Estimativa

**Tempo Total**: 6-8 horas

- Implementação do agregado: 1 hora
- Handlers de eventos: 2 horas
- Cache service: 2 horas
- API e endpoints: 1-2 horas
- Configuração e testes: 1-2 horas

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
