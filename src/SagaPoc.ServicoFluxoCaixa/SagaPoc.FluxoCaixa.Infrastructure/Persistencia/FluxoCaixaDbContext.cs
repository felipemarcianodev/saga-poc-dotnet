using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Infrastructure.Persistencia;

public class FluxoCaixaDbContext : DbContext
{
    public DbSet<Lancamento> Lancamentos { get; set; } = null!;

    public FluxoCaixaDbContext(DbContextOptions<FluxoCaixaDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FluxoCaixaDbContext).Assembly);
    }
}
