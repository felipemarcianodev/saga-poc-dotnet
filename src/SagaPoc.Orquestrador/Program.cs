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
    Log.Information("Iniciando Orquestrador SAGA com RabbitMQ");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== MASSTRANSIT COM RABBITMQ ====================
    builder.Services.AddMassTransit(x =>
    {
        // Configurar SAGA State Machine
        x.AddSagaStateMachine<PedidoSaga, EstadoPedido>()
            .InMemoryRepository(); // Para POC - usar MongoDB/Redis em produção

        // Configurar Dead Letter Queue Consumer
        x.AddConsumer<DeadLetterQueueConsumer>();

        // ==================== RABBITMQ CONFIGURATION ====================
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"]!);
                h.Password(builder.Configuration["RabbitMQ:Password"]!);
            });

            // ============ RETRY POLICY ============
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
                cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                cb.TripThreshold = 15;
                cb.ActiveThreshold = 10;
                cb.ResetInterval = TimeSpan.FromMinutes(5);
            });

            // ============ PREFETCH COUNT ============
            // Limita quantas mensagens cada worker consome simultaneamente
            cfg.PrefetchCount = 16;

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
