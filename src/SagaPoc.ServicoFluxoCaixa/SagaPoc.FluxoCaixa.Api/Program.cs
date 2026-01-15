using Microsoft.EntityFrameworkCore;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.ServiceProvider;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.FluxoCaixa.Infrastructure.Repositorios;
using SagaPoc.FluxoCaixa.Lancamentos.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Configurar DbContext com PostgreSQL
builder.Services.AddDbContext<FluxoCaixaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("FluxoCaixaDb"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null)));

// Repositórios
builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();

// Configurar Rebus
builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(
        $"amqp://{builder.Configuration["RabbitMQ:Username"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:Host"]}:{builder.Configuration["RabbitMQ:Port"]}",
        "fluxocaixa-lancamentos"))
    .Routing(r => r.TypeBased()
        .Map<RegistrarLancamento>("fluxocaixa-lancamentos")));

// Registrar handlers
builder.Services.AutoRegisterHandlersFromAssemblyOf<RegistrarLancamentoHandler>();

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
    var dbContext = scope.ServiceProvider.GetRequiredService<FluxoCaixaDbContext>();
    dbContext.Database.Migrate();
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
