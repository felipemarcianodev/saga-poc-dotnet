using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Serilog;
using SagaPoc.Observability;
using SagaPoc.Common.Mensagens.Comandos;
using Serilog;


try
{
    Log.Information("Iniciando API SAGA POC com Rebus");

    var builder = WebApplication.CreateBuilder(args);

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

    var applicationName = builder.Environment.ApplicationName;

    builder.Services.AddSagaOpenTelemetry(
        builder.Configuration,
        serviceName: applicationName
    );
    
    builder.Host.UseCustomSerilog();

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
