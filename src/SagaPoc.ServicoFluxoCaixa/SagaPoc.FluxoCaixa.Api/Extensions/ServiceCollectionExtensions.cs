using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;
using WebHost.Extensions;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Servicos;
using SagaPoc.FluxoCaixa.Application.Services;

namespace SagaPoc.FluxoCaixa.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddConsolidadoInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext com PostgreSQL
        services.AddDbContext<ConsolidadoDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("ConsolidadoDb"),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null)));

        // Repositorios
        services.AddScoped<IConsolidadoDiarioRepository, ConsolidadoDiarioRepository>();

        return services;
    }

    public static IServiceCollection AddConsolidadoCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Redis Cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = "FluxoCaixa:";
        });

        // Memory Cache
        services.AddMemoryCache();

        // Cache Service
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Response Caching
        services.AddResponseCaching();

        return services;
    }

    public static IServiceCollection AddConsolidadoApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IConsolidadoAppService, ConsolidadoAppService>();
        return services;
    }

    public static IServiceCollection AddConsolidadoRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("consolidado", opt =>
            {
                opt.PermitLimit = 50;
                opt.Window = TimeSpan.FromSeconds(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 10;
            });
        });

        return services;
    }

    public static IServiceCollection AddConsolidadoSwagger(this IServiceCollection services)
    {
        services.AddSwaggerConfiguration(new SwaggerConfiguration
        {
            Title = "API de Consolidado Diario",
            Version = "v1",
            EnableAnnotations = true,
            Description = @"
API para consulta do consolidado diario de fluxo de caixa.

**NFRs Atendidos:**
- 50 requisicoes/segundo
- Latencia P95 < 10ms (com cache)
- Disponibilidade independente do servico de lancamentos

**Endpoints:**
- GET /api/consolidado/{comerciante}/{data} - Consultar consolidado diario
- GET /api/consolidado/{comerciante}/periodo - Consultar consolidado de um periodo

**Cache:**
- Camada 1: Memory Cache (1 minuto)
- Camada 2: Redis Cache (5 minutos)
- Camada 3: PostgreSQL",
            Contact = new OpenApiContact
            {
                Name = "Equipe Backend",
                Email = "backend@empresa.com"
            }
        });

        return services;
    }
}
