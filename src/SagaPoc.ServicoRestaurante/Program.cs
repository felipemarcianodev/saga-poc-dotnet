using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Serilog;
using Rebus.ServiceProvider;
using SagaPoc.Observability;
using SagaPoc.ServicoRestaurante;
using SagaPoc.ServicoRestaurante.Handlers;
using SagaPoc.Shared.Mensagens.Respostas;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Serviço de Restaurante com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar OpenTelemetry
    builder.AddSagaOpenTelemetryForHost(
        serviceName: "SagaPoc.ServicoRestaurante",
        serviceVersion: "1.0.0"
    );

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoRestaurante.Servicos.IServicoRestaurante,
        SagaPoc.ServicoRestaurante.Servicos.ServicoRestaurante>();

    // Registrar repositório de idempotência
    builder.Services.AddSingleton<SagaPoc.Shared.Infraestrutura.IRepositorioIdempotencia,
        SagaPoc.Shared.Infraestrutura.RepositorioIdempotenciaInMemory>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== REBUS COM RABBITMQ ====================

    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fila-restaurante"))
        .Routing(r => r.TypeBased()
            // Rotas para respostas enviadas de volta ao orquestrador
            .Map<PedidoRestauranteValidado>("fila-orquestrador")
            .Map<PedidoRestauranteCancelado>("fila-orquestrador"))
        .Options(o =>
        {
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(10);
        })
    );

    // Registrar handlers automaticamente
    builder.Services.AutoRegisterHandlersFromAssemblyOf<ValidarPedidoRestauranteHandler>();

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    Log.Information("Serviço de Restaurante iniciado com sucesso");

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
