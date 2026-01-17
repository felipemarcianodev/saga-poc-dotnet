using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using SagaPoc.FluxoCaixa.Consolidado.Handlers;
using SagaPoc.FluxoCaixa.Consolidado.Servicos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.Infrastructure.Core;
using Serilog;

try
{
    Log.Information("Iniciando Serviço de Consolidado com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    var applicationName = "SagaPoc.FluxoCaixa.Consolidado";
    var environmentName = builder.Environment.EnvironmentName;

    // Configurar Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.WithProperty("Application", applicationName)
        .Enrich.WithProperty("Environment", environmentName)
        .WriteTo.Console()
        .CreateLogger();

    builder.Services.AddSerilog();

    // Registrar DbContext
    var connectionString = builder.Configuration["ConnectionStrings:ConsolidadoConnection"];
    builder.Services.AddDbContext<ConsolidadoDbContext>(options =>
        options.UseNpgsql(connectionString!));

    // Registrar repositórios
    builder.Services.AddScoped<IConsolidadoDiarioRepository, ConsolidadoDiarioRepository>();

    // Registrar cache
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddScoped<ICacheService, RedisCacheService>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== REBUS COM RABBITMQ ====================

    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fluxocaixa-consolidado"))
        .Options(o =>
        {
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(10);
        })
    );

    // Registrar handlers automaticamente
    builder.Services.AutoRegisterHandlersFromAssemblyOf<LancamentoCreditoRegistradoHandler>();

    var host = builder.Build();

    // Executar migrations
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
        db.Database.Migrate();
    }

    Log.Information("Serviço de Consolidado iniciado com sucesso - Aguardando eventos...");

    host.Run();

    Log.Information("Serviço de Consolidado encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Consolidado falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
