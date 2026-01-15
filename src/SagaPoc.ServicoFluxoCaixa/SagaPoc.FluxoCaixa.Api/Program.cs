using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;
using SagaPoc.FluxoCaixa.Consolidado.Handlers;
using SagaPoc.FluxoCaixa.Consolidado.Servicos;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.FluxoCaixa.Lancamentos.Handlers;
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

// Configurar Rebus
builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(
        $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:{builder.Configuration["RabbitMQ:Port"]}",
        "fluxocaixa-lancamentos"))
    .Routing(r => r.TypeBased()
        .Map<RegistrarLancamento>("fluxocaixa-lancamentos")
        .Map<LancamentoCreditoRegistrado>("fluxocaixa-consolidado")
        .Map<LancamentoDebitoRegistrado>("fluxocaixa-consolidado")));

// Registrar handlers - Lançamentos
builder.Services.AutoRegisterHandlersFromAssemblyOf<RegistrarLancamentoHandler>();

// Registrar handlers - Consolidado
builder.Services.AutoRegisterHandlersFromAssemblyOf<LancamentoCreditoRegistradoHandler>();

// Controllers e Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "API de Fluxo de Caixa",
        Version = "v1",
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
            ",
        Contact = new()
        {
            Name = "Equipe Backend",
            Email = "backend@empresa.com"
        }
    });

    // Incluir comentários XML
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Adicionar servidores
    options.AddServer(new()
    {
        Url = "http://localhost:5000",
        Description = "Desenvolvimento Local"
    });

    options.AddServer(new()
    {
        Url = "https://api-fluxocaixa.empresa.com",
        Description = "Produção"
    });
});

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
app.MapControllers().RequireRateLimiting("consolidado");

app.Run();
