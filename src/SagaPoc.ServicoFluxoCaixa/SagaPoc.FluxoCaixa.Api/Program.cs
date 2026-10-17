using WebHost.Extensions;
using SagaPoc.FluxoCaixa.Api.Extensions;
using SagaPoc.Observability;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services
    .AddConsolidadoInfrastructure(builder.Configuration)
    .AddConsolidadoCaching(builder.Configuration)
    .AddConsolidadoApplicationServices()
    .AddConsolidadoRateLimiting()
    .AddConsolidadoSwagger()
    .AddHealthChecks();

builder.Services.AddControllers();

// Observability
builder.Services.AddSagaOpenTelemetry(builder.Configuration, builder.Environment.ApplicationName);
builder.Host.UseCustomSerilog();

var app = builder.Build();

// O schema do consolidado é migrado pelo SagaPoc.FluxoCaixa.Consolidado, dono da escrita.

// Pipeline
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseResponseCaching();
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("consolidado");

app.Run();
