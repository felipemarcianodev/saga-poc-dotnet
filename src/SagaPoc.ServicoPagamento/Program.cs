using MassTransit;
using SagaPoc.ServicoPagamento;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Serviço de Pagamento");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoPagamento.Servicos.IServicoPagamento, SagaPoc.ServicoPagamento.Servicos.ServicoPagamento>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // Registrar consumers
        x.AddConsumer<SagaPoc.ServicoPagamento.Consumers.ProcessarPagamentoConsumer>();
        x.AddConsumer<SagaPoc.ServicoPagamento.Consumers.EstornarPagamentoConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            // Configurar endpoint para este serviço
            cfg.ReceiveEndpoint("fila-pagamento", e =>
            {
                // Configurar consumers neste endpoint
                e.ConfigureConsumer<SagaPoc.ServicoPagamento.Consumers.ProcessarPagamentoConsumer>(context);
                e.ConfigureConsumer<SagaPoc.ServicoPagamento.Consumers.EstornarPagamentoConsumer>(context);
            });
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    host.Run();

    Log.Information("Serviço de Pagamento encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Pagamento falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
