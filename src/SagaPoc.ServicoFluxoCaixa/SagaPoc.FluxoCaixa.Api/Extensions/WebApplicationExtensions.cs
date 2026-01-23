using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;

namespace SagaPoc.FluxoCaixa.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication MigrateConsolidadoDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>();
        dbContext.Database.Migrate();
        return app;
    }
}
