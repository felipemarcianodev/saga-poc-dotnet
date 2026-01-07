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

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoRestaurante.Servicos.IServicoRestaurante, SagaPoc.ServicoRestaurante.Servicos.ServicoRestaurante>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // Registrar consumers
        x.AddConsumer<SagaPoc.ServicoRestaurante.Consumers.ValidarPedidoRestauranteConsumer>();
        x.AddConsumer<SagaPoc.ServicoRestaurante.Consumers.CancelarPedidoRestauranteConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            // Configurar endpoint para este serviço
            cfg.ReceiveEndpoint("fila-restaurante", e =>
            {
                // Configurar consumers neste endpoint
                e.ConfigureConsumer<SagaPoc.ServicoRestaurante.Consumers.ValidarPedidoRestauranteConsumer>(context);
                e.ConfigureConsumer<SagaPoc.ServicoRestaurante.Consumers.CancelarPedidoRestauranteConsumer>(context);
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
