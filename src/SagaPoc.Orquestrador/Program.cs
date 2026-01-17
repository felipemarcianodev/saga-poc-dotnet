using Microsoft.EntityFrameworkCore;
using Polly;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Observability;
using SagaPoc.Orquestrador;
using SagaPoc.Orquestrador.Persistence;
using SagaPoc.Orquestrador.Sagas;
using Serilog;

try
{
    Log.Information("Iniciando Orquestrador SAGA com Rebus + RabbitMQ");

    var builder = Host.CreateApplicationBuilder(args);

    var applicationName = builder.Environment.ApplicationName;
    var environmentName = builder.Environment.EnvironmentName;
    var isDevelopment = builder.Environment.IsDevelopment();

    builder.AddSagaOpenTelemetryForHost(applicationName);
    builder.UseCustomSerilog(builder.Configuration, applicationName, environmentName, builder.Environment.IsDevelopment());
    

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // ==================== ENTITY FRAMEWORK CORE ====================

    // Configurar DbContext para persistência do estado da SAGA
    var connectionString = builder.Configuration.GetConnectionString("SagaDatabase")
        ?? throw new InvalidOperationException("Connection string 'SagaDatabase' não encontrada.");

    builder.Services.AddDbContext<SagaDbContext>(options =>
    {
        options.UseNpgsql(connectionString);
    });

    // ==================== REBUS COM RABBITMQ ====================

    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fila-orquestrador"))
        .Sagas(s => s.StoreInPostgres(
            connectionString,
            "PedidoSagas",        // Nome da tabela de dados da SAGA
            "PedidoSagasIndex"))  // Nome da tabela de índices da SAGA
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
            // Retry policy nativo do Rebus irá lidar com falhas e concorrência
        })
    );

    // Registrar a SAGA
    builder.Services.AutoRegisterHandlersFromAssemblyOf<PedidoSaga>();

    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    // Garantir que o banco de dados e as tabelas sejam criados
    await host.EnsureDatabaseCreatedAsync();

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