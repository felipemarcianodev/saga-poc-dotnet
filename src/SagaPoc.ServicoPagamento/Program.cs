using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Serilog;
using Rebus.ServiceProvider;
using SagaPoc.Observability;
using SagaPoc.ServicoPagamento;
using SagaPoc.ServicoPagamento.Handlers;
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
    Log.Information("Iniciando Serviço de Pagamento com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Configurar OpenTelemetry
    builder.AddSagaOpenTelemetryForHost(
        serviceName: "SagaPoc.ServicoPagamento",
        serviceVersion: "1.0.0"
    );

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoPagamento.Servicos.IServicoPagamento,
        SagaPoc.ServicoPagamento.Servicos.ServicoPagamento>();

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
            "fila-pagamento"))
        .Routing(r => r.TypeBased()
            .Map<PagamentoProcessado>("fila-orquestrador")
            .Map<PagamentoEstornado>("fila-orquestrador"))
        .Options(o =>
        {
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(10);
        })
    );

    // Registrar handlers automaticamente
    builder.Services.AutoRegisterHandlersFromAssemblyOf<ProcessarPagamentoHandler>();

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    Log.Information("Serviço de Pagamento iniciado com sucesso");

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
