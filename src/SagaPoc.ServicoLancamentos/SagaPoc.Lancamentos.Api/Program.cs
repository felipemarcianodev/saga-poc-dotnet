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

// O schema de lançamentos é migrado pelo SagaPoc.Lancamentos.Worker, dono da escrita.

// Pipeline
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sem HTTPS no container local: o compose passa Https__Redirecionar=false.
if (app.Configuration.GetValue("Https:Redirecionar", true))
    app.UseHttpsRedirection();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
