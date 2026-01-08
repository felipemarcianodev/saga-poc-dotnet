using MassTransit;
using SagaPoc.ServicoEntregador;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Iniciando Serviço de Entregador");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog como provedor de logging
    builder.Services.AddSerilog();

    // Registrar serviços de negócio
    builder.Services.AddScoped<SagaPoc.ServicoEntregador.Servicos.IServicoEntregador, SagaPoc.ServicoEntregador.Servicos.ServicoEntregador>();

    // Registrar repositório de idempotência
    builder.Services.AddSingleton<SagaPoc.Shared.Infraestrutura.IRepositorioIdempotencia, SagaPoc.Shared.Infraestrutura.RepositorioIdempotenciaInMemory>();

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== MASSTRANSIT COM RABBITMQ ====================
    builder.Services.AddMassTransit(x =>
    {
        // Registrar consumers
        x.AddConsumer<SagaPoc.ServicoEntregador.Consumers.AlocarEntregadorConsumer>();
        x.AddConsumer<SagaPoc.ServicoEntregador.Consumers.LiberarEntregadorConsumer>();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"]!);
                h.Password(builder.Configuration["RabbitMQ:Password"]!);
            });

            // Retry policy
            cfg.UseMessageRetry(retry =>
            {
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2));
                retry.Handle<TimeoutException>();
            });

            // ============ FILA ESPECÍFICA DO ENTREGADOR ============
            cfg.ReceiveEndpoint("fila-entregador", e =>
            {
                e.ConfigureConsumer<SagaPoc.ServicoEntregador.Consumers.AlocarEntregadorConsumer>(context);
                e.ConfigureConsumer<SagaPoc.ServicoEntregador.Consumers.LiberarEntregadorConsumer>(context);

                // Configurações de performance
                e.PrefetchCount = 16;
                e.UseConcurrencyLimit(10); // Máximo 10 mensagens processadas simultaneamente
            });
        });
    });

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    host.Run();

    Log.Information("Serviço de Entregador encerrado");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Serviço de Entregador falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
