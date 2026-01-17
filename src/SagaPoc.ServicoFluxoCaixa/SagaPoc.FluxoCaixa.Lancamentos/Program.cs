using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.FluxoCaixa.Lancamentos.Handlers;
using SagaPoc.Observability;
using Serilog;

try
{
    Log.Information("Iniciando Serviço de Lançamentos com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    var applicationName = builder.Environment.ApplicationName;
    var environmentName = builder.Environment.EnvironmentName;
    var isDevelopment = builder.Environment.IsDevelopment();

    builder.AddSagaOpenTelemetryForHost(applicationName);
    builder.UseCustomSerilog(builder.Configuration, applicationName, environmentName, builder.Environment.IsDevelopment());

    // Registrar DbContext
    var connectionString = builder.Configuration["ConnectionStrings:FluxoCaixaConnection"];
    builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
        options.UseNpgsql(connectionString!));

    // Registrar repositórios
    builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== REBUS COM RABBITMQ ====================

    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fluxocaixa-lancamentos"))
        .Routing(r => r.TypeBased()
            // Rotas para eventos enviados ao serviço de consolidado
            .Map<LancamentoCreditoRegistrado>("fluxocaixa-consolidado")
            .Map<LancamentoDebitoRegistrado>("fluxocaixa-consolidado"))
        .Options(o =>
        {
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(10);
        })
    );

    // Registrar handlers automaticamente
    builder.Services.AutoRegisterHandlersFromAssemblyOf<RegistrarLancamentoHandler>();

    var host = builder.Build();

    // Executar migrations
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<FluxoCaixaDbContext>();
        db.Database.Migrate();
    }

    Log.Information("Serviço de Lançamentos iniciado com sucesso - Aguardando mensagens...");

    host.Run();

    Log.Information("Serviço de Lançamentos encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Lançamentos falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
