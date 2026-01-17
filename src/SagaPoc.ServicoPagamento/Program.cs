using Rebus.Config;
using Rebus.Routing.TypeBased;
using SagaPoc.Infrastructure.Core;
using SagaPoc.Observability;
using SagaPoc.ServicoPagamento;
using SagaPoc.ServicoPagamento.Handlers;
using SagaPoc.Common.Mensagens.Respostas;
using Serilog;

try
{
    Log.Information("Iniciando Serviço de Pagamento com Rebus");

    var builder = Host.CreateApplicationBuilder(args);
    var applicationName = builder.Environment.ApplicationName;
    var environmentName = builder.Environment.EnvironmentName;
    var isDevelopment = builder.Environment.IsDevelopment();

    builder.AddSagaOpenTelemetryForHost(applicationName);
    builder.UseCustomSerilog(builder.Configuration, applicationName, environmentName, builder.Environment.IsDevelopment());

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoPagamento.Servicos.IServicoPagamento,
        SagaPoc.ServicoPagamento.Servicos.ServicoPagamento>();

    // Registrar repositório de idempotência
    builder.Services.AddSingleton<IRepositorioIdempotencia,
        RepositorioIdempotenciaInMemory>();

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
