using WebHost.Extensions;
using SagaPoc.Lancamentos.Api.Extensions;
using SagaPoc.Observability;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services
    .AddLancamentosInfrastructure(builder.Configuration)
    .AddLancamentosApplicationServices()
    .AddLancamentosMessaging(builder.Configuration)
    .AddLancamentosSwagger()
    .AddHealthChecks();

builder.Services.AddControllers();

// Observability
builder.Services.AddSagaOpenTelemetry(builder.Configuration, builder.Environment.ApplicationName);
builder.Host.UseCustomSerilog();

var app = builder.Build();

// Startup
app.MigrateLancamentosDatabase();

// Pipeline
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
