using MassTransit;
using SagaPoc.ServicoRestaurante;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Serviço de Restaurante");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // Os consumers serão adicionados na Fase 4
        // x.AddConsumer<ValidarPedidoRestauranteConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            // Configurar endpoint para este serviço
            cfg.ReceiveEndpoint("fila-restaurante", e =>
            {
                // Os consumers serão configurados aqui na Fase 4
                // e.ConfigureConsumer<ValidarPedidoRestauranteConsumer>(context);
            });
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    host.Run();

    Log.Information("Serviço de Restaurante encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Restaurante falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
