using MassTransit;
using SagaPoc.ServicoEntregador;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Serviço de Entregador");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoEntregador.Servicos.IServicoEntregador, SagaPoc.ServicoEntregador.Servicos.ServicoEntregador>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // Registrar consumers
        x.AddConsumer<SagaPoc.ServicoEntregador.Consumers.AlocarEntregadorConsumer>();
        x.AddConsumer<SagaPoc.ServicoEntregador.Consumers.LiberarEntregadorConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            // Configurar endpoint para este serviço
            cfg.ReceiveEndpoint("fila-entregador", e =>
            {
                // Configurar consumers neste endpoint
                e.ConfigureConsumer<SagaPoc.ServicoEntregador.Consumers.AlocarEntregadorConsumer>(context);
                e.ConfigureConsumer<SagaPoc.ServicoEntregador.Consumers.LiberarEntregadorConsumer>(context);
            });
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    host.Run();

    Log.Information("Serviço de Entregador encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Entregador falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
