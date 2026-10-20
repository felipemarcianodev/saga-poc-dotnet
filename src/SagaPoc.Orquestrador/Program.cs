using Rebus.Config;
using Rebus.Routing.TypeBased;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Observability;
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

    // Estado da SAGA persistido pelo próprio Rebus no PostgreSQL. O Rebus cria e
    // mantém as tabelas (id, revision, data em JSON gravado como bytea + índice de correlação);
    // nenhum outro componente deve criar ou alterar essas tabelas.
    var connectionString = builder.Configuration.GetConnectionString("SagaDatabase")
        ?? throw new InvalidOperationException("Connection string 'SagaDatabase' não encontrada.");

    // ==================== REBUS COM RABBITMQ ====================

    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMq(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}",
            "fila-orquestrador"))
        .Sagas(s => s.StoreInPostgres(
            connectionString,
            "pedido_saga_data",   // Tabela de dados da SAGA
            "pedido_saga_index",  // Tabela de índices de correlação
            automaticallyCreateTables: true))
        // Mensagens adiadas (prazos da SAGA e novas tentativas de compensação) ficam no
        // PostgreSQL até a hora de entrega: o transporte do RabbitMQ não adia mensagens sozinho.
        .Timeouts(t => t.StoreInPostgres(
            connectionString,
            "rebus_timeouts",
            automaticallyCreateTables: true))
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

    builder.Services.Configure<OpcoesSaga>(builder.Configuration.GetSection(OpcoesSaga.Secao));

    // Registrar a SAGA
    builder.Services.AutoRegisterHandlersFromAssemblyOf<PedidoSaga>();

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