using Rebus.Config;
using Rebus.Routing.TypeBased;
using SagaPoc.Observability;
using SagaPoc.ServicoNotificacao;
using SagaPoc.ServicoNotificacao.Handlers;
using SagaPoc.Common.Mensagens.Respostas;
using Serilog;

try
{
    Log.Information("Iniciando Serviço de Notificação com Rebus");

    var builder = Host.CreateApplicationBuilder(args);

    var applicationName = builder.Environment.ApplicationName;
    var environmentName = builder.Environment.EnvironmentName;
    var isDevelopment = builder.Environment.IsDevelopment();

    builder.AddSagaOpenTelemetryForHost(applicationName);
    builder.UseCustomSerilog(builder.Configuration, applicationName, environmentName, builder.Environment.IsDevelopment());

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoNotificacao.Servicos.IServicoNotificacao,
        SagaPoc.ServicoNotificacao.Servicos.ServicoNotificacao>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== REBUS COM RABBITMQ ====================
    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fila-notificacao"))
        .Routing(r => r.TypeBased()
            .Map<NotificacaoEnviada>("fila-orquestrador"))
        .Options(o =>
        {
            o.SetNumberOfWorkers(1);
            o.SetMaxParallelism(10);
        })
    );

    // Registrar handlers automaticamente
    builder.Services.AutoRegisterHandlersFromAssemblyOf<NotificarClienteHandler>();

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    Log.Information("Serviço de Notificação iniciado com sucesso");

    host.Run();

    Log.Information("Serviço de Notificação encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Notificação falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
