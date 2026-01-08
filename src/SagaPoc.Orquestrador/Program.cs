using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Routing.TypeBased;
using Rebus.Sagas;
using Rebus.Serilog;
using Rebus.ServiceProvider;
using SagaPoc.Observability;
using SagaPoc.Orquestrador;
using SagaPoc.Orquestrador.Sagas;
using SagaPoc.Shared.Mensagens.Comandos;
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
    Log.Information("Iniciando Orquestrador SAGA com Rebus + RabbitMQ");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar OpenTelemetry
    builder.AddSagaOpenTelemetryForHost(
        serviceName: "SagaPoc.Orquestrador",
        serviceVersion: "1.0.0"
    );

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== REBUS COM RABBITMQ ====================

    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fila-orquestrador"))
        .Sagas(s => s.StoreInMemory()) // Para POC - usar SQL/MongoDB em produção
        .Routing(r => r.TypeBased()
            // Rotas para comandos enviados pela SAGA
            .Map<ValidarPedidoRestaurante>("fila-restaurante")
            .Map<ProcessarPagamento>("fila-pagamento")
            .Map<AlocarEntregador>("fila-entregador")
            .Map<NotificarCliente>("fila-notificacao")
            .Map<CancelarPedidoRestaurante>("fila-restaurante")
            .Map<EstornarPagamento>("fila-pagamento")
            .Map<LiberarEntregador>("fila-entregador"))
        .Options(o =>
        {
            o.SetNumberOfWorkers(1); // Número de workers processando mensagens
            o.SetMaxParallelism(10); // Máximo de mensagens processadas em paralelo
        })
    );

    // Registrar a SAGA
    builder.Services.AutoRegisterHandlersFromAssemblyOf<PedidoSaga>();

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    Log.Information("Orquestrador SAGA iniciado com sucesso");

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
