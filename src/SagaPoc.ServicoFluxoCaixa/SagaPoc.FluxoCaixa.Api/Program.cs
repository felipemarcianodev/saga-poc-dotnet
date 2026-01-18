using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Rebus.Config;
using WebHost.Extensions;
using Rebus.Handlers;
using Rebus.Routing.TypeBased;
using SagaPoc.FluxoCaixa.Consolidado.Handlers;
using SagaPoc.FluxoCaixa.Consolidado.Servicos;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.FluxoCaixa.Lancamentos.Handlers;
using SagaPoc.Observability;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configurar DbContext com PostgreSQL - Lançamentos
builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("FluxoCaixaDb"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null)));

// Configurar DbContext com PostgreSQL - Consolidado
builder.Services.AddDbContext<ConsolidadoDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("ConsolidadoDb"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null)));

// Repositórios
builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();
builder.Services.AddScoped<IConsolidadoDiarioRepository, ConsolidadoDiarioRepository>();

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "FluxoCaixa:";
});

// Memory Cache
builder.Services.AddMemoryCache();

// Cache Service
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// Response Caching
builder.Services.AddResponseCaching();

// Rate Limiting (NFR: 50 req/s)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("consolidado", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromSeconds(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Configurar Rebus - escuta múltiplas filas
builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(
        $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:{builder.Configuration["RabbitMQ:Port"]}",
        "fluxocaixa-lancamentos"))
    .Routing(r => r.TypeBased()
        .Map<RegistrarLancamento>("fluxocaixa-lancamentos")
        .Map<LancamentoCreditoRegistrado>("fluxocaixa-consolidado")
        .Map<LancamentoDebitoRegistrado>("fluxocaixa-consolidado")),
    onCreated: async bus =>
    {
        // Subscrever também à fila de consolidado para processar eventos
        await bus.Subscribe<LancamentoCreditoRegistrado>();
        await bus.Subscribe<LancamentoDebitoRegistrado>();
    });

// Registrar handlers manualmente - Lançamentos
builder.Services.AddScoped<IHandleMessages<RegistrarLancamento>, RegistrarLancamentoHandler>();

// Registrar handler para mensagens de resposta (limpar fila de mensagens antigas)
builder.Services.AddScoped<IHandleMessages<SagaPoc.FluxoCaixa.Domain.Respostas.LancamentoRegistradoComSucesso>, SagaPoc.FluxoCaixa.Lancamentos.Handlers.LancamentoRegistradoComSucessoHandler>();

// Registrar handlers manualmente - Consolidado
builder.Services.AddScoped<IHandleMessages<LancamentoCreditoRegistrado>, LancamentoCreditoRegistradoHandler>();
builder.Services.AddScoped<IHandleMessages<LancamentoDebitoRegistrado>, LancamentoDebitoRegistradoHandler>();

// Health Checks
builder.Services.AddHealthChecks();

// Controllers e Swagger
builder.Services.AddControllers();
builder.Services.AddSwaggerConfiguration(new SwaggerConfiguration
{
    Title = "API de Fluxo de Caixa",
    Version = "v1",
    EnableAnnotations = true,
    Description = @"
API para controle de fluxo de caixa com lançamentos e consolidado diário.

**Arquitetura:** CQRS + Event-Driven

**NFRs Atendidos:**
- 50 requisições/segundo no consolidado
- Disponibilidade independente entre serviços
- < 5% de perda de requisições
- Latência P95 < 10ms (com cache)

**Endpoints Principais:**
- POST /api/lancamentos - Registrar lançamento (débito ou crédito)
- GET /api/consolidado/{comerciante}/{data} - Consultar consolidado diário
- GET /api/consolidado/{comerciante}/periodo - Consultar consolidado de um período

**Tipos de Lançamento:**
- 1 = Débito (saída de caixa)
- 2 = Crédito (entrada de caixa)

**Status de Lançamento:**
- Pendente = Aguardando processamento
- Confirmado = Processado com sucesso
- Cancelado = Lançamento cancelado",
    Contact = new OpenApiContact
    {
        Name = "Equipe Backend",
        Email = "backend@empresa.com"
    }
});

var applicationName = builder.Environment.ApplicationName;
builder.Services.AddSagaOpenTelemetry(
       builder.Configuration,
       serviceName: applicationName
   );

builder.Host.UseCustomSerilog();

var app = builder.Build();

// Aplicar migrations automaticamente
using (var scope = app.Services.CreateScope())
{
    var fluxoCaixaDbContext = scope.ServiceProvider.GetRequiredService<FluxoCaixaDbContext>();
    fluxoCaixaDbContext.Database.Migrate();

    var consolidadoDbContext = scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
    consolidadoDbContext.Database.Migrate();
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseRateLimiter();
app.UseAuthorization();

// Health Check endpoint
app.MapHealthChecks("/health");

app.MapControllers().RequireRateLimiting("consolidado");

app.Run();
