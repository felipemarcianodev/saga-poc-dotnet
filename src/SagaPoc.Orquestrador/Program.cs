using MassTransit;
using SagaPoc.Orquestrador;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Orquestrador SAGA");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // A SAGA State Machine será adicionada na Fase 3
        // x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
        //     .InMemoryRepository();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    host.Run();

    Log.Information("Orquestrador SAGA encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Orquestrador SAGA falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
