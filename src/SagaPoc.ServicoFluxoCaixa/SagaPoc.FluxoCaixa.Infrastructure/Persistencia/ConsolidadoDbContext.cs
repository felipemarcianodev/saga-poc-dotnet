using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Infrastructure.Persistencia;

public class ConsolidadoDbContext : DbContext
{
    public DbSet<ConsolidadoDiario> Consolidados { get; set; } = null!;

    public ConsolidadoDbContext(DbContextOptions<ConsolidadoDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConsolidadoDiario>(entity =>
        {
            entity.ToTable("ConsolidadosDiarios");

            entity.HasKey(c => c.Id);

            entity.HasIndex(c => new { c.Data, c.Comerciante })
                .IsUnique()
                .HasDatabaseName("IX_Consolidado_Data_Comerciante");

            entity.Property(c => c.Comerciante)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(c => c.TotalCreditos)
                .HasColumnType("decimal(18,2)");

            entity.Property(c => c.TotalDebitos)
                .HasColumnType("decimal(18,2)");

            entity.Ignore(c => c.SaldoDiario);
            entity.Ignore(c => c.QuantidadeTotalLancamentos);
        });
    }
}
