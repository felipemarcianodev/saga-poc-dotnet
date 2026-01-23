using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using SagaPoc.FluxoCaixa.Application.Services;
using SagaPoc.FluxoCaixa.Consolidado.Handlers;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Servicos;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.Infrastructure.Core;
using Serilog;
using WebHost.Extensions;

try
{
    Log.Information("Iniciando Servico de Consolidado com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    var applicationName = "SagaPoc.FluxoCaixa.Consolidado";
    var environmentName = builder.Environment.EnvironmentName;

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.WithProperty("Application", applicationName)
        .Enrich.WithProperty("Environment", environmentName)
        .WriteTo.Console()
        .CreateLogger();

    builder.Services.AddSerilog();

    builder.Services
        .AddPostgreSqlDbContext<ConsolidadoDbContext>(
            builder.Configuration["ConnectionStrings:ConsolidadoConnection"]!)
        .AddScoped<IConsolidadoDiarioRepository, ConsolidadoDiarioRepository>()
        .AddScoped<IUnitOfWork, UnitOfWork<ConsolidadoDbContext>>()
        .AddDistributedMemoryCache()
        .AddScoped<ICacheService, RedisCacheService>();

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    builder.Services.AddRebusWithRabbitMq(
        new RebusConfiguration
        {
            Host = builder.Configuration["RabbitMQ:Host"]!,
            Username = builder.Configuration["RabbitMQ:Username"]!,
            Password = builder.Configuration["RabbitMQ:Password"]!,
            QueueName = "fluxocaixa-consolidado"
        });

    builder.Services.AutoRegisterHandlersFromAssemblyOf<LancamentoCreditoRegistradoHandler>();

    var host = builder.Build();
    host.MigrateDatabase<ConsolidadoDbContext>();

    Log.Information("Servico de Consolidado iniciado com sucesso - Aguardando eventos...");
    host.Run();
    Log.Information("Servico de Consolidado encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Servico de Consolidado falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
