using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Serilog;
using SagaPoc.Observability;
using SagaPoc.Shared.Mensagens.Comandos;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Iniciando API SAGA POC com Rebus");

    var builder = WebApplication.CreateBuilder(args);

    // Configurar Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() {
            Title = "SAGA POC - Sistema de Delivery",
            Version = "v1",
            Description = "POC de SAGA Pattern com Rebus e RabbitMQ para sistema de delivery de comida"
        });

        // Incluir comentários XML na documentação
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });

    // ==================== REBUS COM RABBITMQ (ONE-WAY CLIENT) ====================
    // A API apenas envia mensagens, não recebe (One-Way Client)
    builder.Services.AddRebus((configure, provider) => configure
        .Logging(l => l.Serilog())
        .Transport(t => t.UseRabbitMqAsOneWayClient(
            $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}"))
        .Routing(r => r.TypeBased()
            .Map<IniciarPedido>("fila-orquestrador"))
    );

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    // Configurar OpenTelemetry
    builder.Services.AddSagaOpenTelemetry(
        builder.Configuration,
        serviceName: "SagaPoc.Api",
        serviceVersion: "1.0.0"
    );

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "SAGA POC API v1");
            c.RoutePrefix = string.Empty; // Swagger na raiz
        });
    }

    app.UseSerilogRequestLogging();

    // Habilitar endpoint de métricas OpenTelemetry/Prometheus
    app.UseSagaOpenTelemetry();

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health");

    app.Run();

    Log.Information("API SAGA POC encerrada");
}
catch (Exception ex)
{
    Log.Fatal(ex, "API SAGA POC falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
