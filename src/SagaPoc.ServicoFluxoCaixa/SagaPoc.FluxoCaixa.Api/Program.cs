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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FluxoCaixa.Api", Version = "v1" });
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
