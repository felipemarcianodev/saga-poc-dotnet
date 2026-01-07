using MassTransit;
using SagaPoc.Orquestrador;
using SagaPoc.Orquestrador.Consumers;
using SagaPoc.Orquestrador.Sagas;
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
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy())
        .AddAzureServiceBusQueue(
            builder.Configuration["AzureServiceBus:ConnectionString"]!,
            queueName: "fila-restaurante",
            name: "azure-servicebus-restaurante"
        )
        .AddAzureServiceBusQueue(
            builder.Configuration["AzureServiceBus:ConnectionString"]!,
            queueName: "fila-pagamento",
            name: "azure-servicebus-pagamento"
        )
        .AddAzureServiceBusQueue(
            builder.Configuration["AzureServiceBus:ConnectionString"]!,
            queueName: "fila-entregador",
            name: "azure-servicebus-entregador"
        );

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // Configurar SAGA State Machine para orquestração de pedidos
        x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
            .InMemoryRepository(); // Para POC - usar Redis/SQL em produção

        // Configurar Dead Letter Queue Consumer
        x.AddConsumer<DeadLetterQueueConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            // ============ RETRY POLICY ============
            // Configuração de retry com exponential backoff
            cfg.UseMessageRetry(retry =>
            {
                retry.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(2)
                );

                // Retry apenas em erros transitórios
                retry.Handle<TimeoutException>();
                retry.Handle<HttpRequestException>();
            });

            // ============ CIRCUIT BREAKER ============
            cfg.UseCircuitBreaker(cb =>
            {
                // Abrir circuito após 15 falhas em 1 minuto
                cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                cb.TripThreshold = 15;
                cb.ActiveThreshold = 10;

                // Fechar circuito após 5 minutos sem falhas
                cb.ResetInterval = TimeSpan.FromMinutes(5);
            });

            // ============ DEAD LETTER QUEUE ============
            cfg.ReceiveEndpoint("fila-dead-letter", e =>
            {
                e.ConfigureConsumer<DeadLetterQueueConsumer>(context);
            });

            // Configurar endpoints automaticamente
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
