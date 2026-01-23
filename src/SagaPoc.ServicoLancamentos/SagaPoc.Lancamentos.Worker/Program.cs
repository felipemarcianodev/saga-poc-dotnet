using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Config;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.Lancamentos.Worker.Handlers;
using SagaPoc.Observability;
using Serilog;
using WebHost.Extensions;

try
{
    Log.Information("Iniciando Servico de Lancamentos com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    var applicationName = builder.Environment.ApplicationName;
    var environmentName = builder.Environment.EnvironmentName;

    builder.AddSagaOpenTelemetryForHost(applicationName);
    builder.UseCustomSerilog(builder.Configuration, applicationName, environmentName, builder.Environment.IsDevelopment());

    builder.Services
        .AddPostgreSqlDbContext<FluxoCaixaDbContext>(
            builder.Configuration["ConnectionStrings:FluxoCaixaConnection"]!)
        .AddScoped<ILancamentoRepository, LancamentoRepository>()
        .AddScoped<IUnitOfWork, UnitOfWork<FluxoCaixaDbContext>>()
        .AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    builder.Services.AddRebusWithRabbitMq(
        new RebusConfiguration
        {
            Host = builder.Configuration["RabbitMQ:Host"]!,
            Username = builder.Configuration["RabbitMQ:Username"]!,
            Password = builder.Configuration["RabbitMQ:Password"]!,
            QueueName = "fluxocaixa-lancamentos"
        },
        routing => routing
            .Map<LancamentoCreditoRegistrado>("fluxocaixa-consolidado")
            .Map<LancamentoDebitoRegistrado>("fluxocaixa-consolidado"));

    builder.Services.AutoRegisterHandlersFromAssemblyOf<RegistrarLancamentoHandler>();

    var host = builder.Build();
    host.MigrateDatabase<FluxoCaixaDbContext>();

    Log.Information("Servico de Lancamentos iniciado com sucesso - Aguardando mensagens...");
    host.Run();
    Log.Information("Servico de Lancamentos encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Servico de Lancamentos falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
