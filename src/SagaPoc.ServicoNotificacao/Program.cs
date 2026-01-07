using MassTransit;
using SagaPoc.ServicoNotificacao;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Serviço de Notificação");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoNotificacao.Servicos.IServicoNotificacao, SagaPoc.ServicoNotificacao.Servicos.ServicoNotificacao>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar MassTransit com Azure Service Bus
    builder.Services.AddMassTransit(x =>
    {
        // Registrar consumer
        x.AddConsumer<SagaPoc.ServicoNotificacao.Consumers.NotificarClienteConsumer>();

        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(builder.Configuration["AzureServiceBus:ConnectionString"]);

            // Configurar endpoint para este serviço
            cfg.ReceiveEndpoint("fila-notificacao", e =>
            {
                // Configurar consumer neste endpoint
                e.ConfigureConsumer<SagaPoc.ServicoNotificacao.Consumers.NotificarClienteConsumer>(context);
            });
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

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
