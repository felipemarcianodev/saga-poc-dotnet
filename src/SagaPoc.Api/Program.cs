using MassTransit;
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
    Log.Information("Iniciando API SAGA POC");

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
            Description = "POC de SAGA Pattern com MassTransit e Azure Service Bus para sistema de delivery de comida"
        });

        // Incluir comentários XML na documentação
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });

    // Configurar MassTransit com Azure Service Bus (somente se connection string válida)
    var azureServiceBusConnectionString = builder.Configuration["AzureServiceBus:ConnectionString"];
    var isValidConnectionString = !string.IsNullOrWhiteSpace(azureServiceBusConnectionString)
                                   && !azureServiceBusConnectionString.Contains("[seu-namespace]")
                                   && !azureServiceBusConnectionString.Contains("[sua-chave]");

    if (isValidConnectionString)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.UsingAzureServiceBus((context, cfg) =>
            {
                cfg.Host(azureServiceBusConnectionString);

                // A API não precisa de receive endpoints, apenas publica mensagens
            });
        });
    }
    else
    {
        // Modo demo: MassTransit desabilitado
        Log.Warning("Azure Service Bus não configurado. API rodando em modo DEMO (sem publicação de mensagens).");
    }

    // Configurar Health Checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

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
