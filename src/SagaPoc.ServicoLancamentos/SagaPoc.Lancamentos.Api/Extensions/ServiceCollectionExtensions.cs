using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using WebHost.Extensions;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.FluxoCaixa.Application.Services;

namespace SagaPoc.Lancamentos.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLancamentosInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext com PostgreSQL
        services.AddDbContext<FluxoCaixaDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("LancamentosDb"),
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null)));

        // Repositorios e Unit of Work
        services.AddScoped<ILancamentoRepository, LancamentoRepository>();
        services.AddScoped<IUnitOfWork>(sp => new UnitOfWork<FluxoCaixaDbContext>(
            sp.GetRequiredService<FluxoCaixaDbContext>()));

        return services;
    }

    public static IServiceCollection AddLancamentosApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ILancamentoAppService, LancamentoAppService>();
        return services;
    }

    public static IServiceCollection AddLancamentosMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rabbitHost = configuration["RabbitMQ:Host"];
        var rabbitPort = configuration["RabbitMQ:Port"];
        var rabbitUser = configuration["RabbitMQ:Username"];
        var rabbitPass = configuration["RabbitMQ:Password"];

        services.AddRebus(configure => configure
            .Transport(t => t.UseRabbitMq(
                $"amqp://{rabbitUser}:{rabbitPass}@{rabbitHost}:{rabbitPort}",
                "lancamentos-api"))
            .Routing(r => r.TypeBased()
                .Map<RegistrarLancamento>("fluxocaixa-lancamentos")));

        return services;
    }

    public static IServiceCollection AddLancamentosSwagger(this IServiceCollection services)
    {
        services.AddSwaggerConfiguration(new SwaggerConfiguration
        {
            Title = "API de Lancamentos",
            Version = "v1",
            EnableAnnotations = true,
            Description = @"
API para registro e consulta de lancamentos financeiros.

**Endpoints:**
- POST /api/lancamentos - Registrar lancamento (debito ou credito)
- GET /api/lancamentos/{id} - Consultar lancamento por ID
- GET /api/lancamentos - Listar lancamentos por periodo

**Tipos de Lancamento:**
- 1 = Debito (saida de caixa)
- 2 = Credito (entrada de caixa)

**Status de Lancamento:**
- Pendente = Aguardando processamento
- Confirmado = Processado com sucesso
- Cancelado = Lancamento cancelado",
            Contact = new OpenApiContact
            {
                Name = "Equipe Backend",
                Email = "backend@empresa.com"
            }
        });

        return services;
    }
}
