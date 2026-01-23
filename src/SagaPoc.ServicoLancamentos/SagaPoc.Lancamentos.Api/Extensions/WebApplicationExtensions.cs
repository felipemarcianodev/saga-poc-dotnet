using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;

namespace SagaPoc.Lancamentos.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MigrateLancamentosDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FluxoCaixaDbContext>();
        dbContext.Database.Migrate();
        return app;
    }
}
