using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Infrastructure.Persistencia.Configuracoes;

public class LancamentoConfiguration : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        builder.ToTable("lancamentos");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.Tipo)
            .HasColumnName("tipo")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Valor)
            .HasColumnName("valor")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(l => l.DataLancamento)
            .HasColumnName("data_lancamento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(l => l.Comerciante)
            .HasColumnName("comerciante")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Categoria)
            .HasColumnName("categoria")
            .HasMaxLength(100);

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(l => l.AtualizadoEm)
            .HasColumnName("atualizado_em");

        // Índices para otimização de consultas
        builder.HasIndex(l => new { l.Comerciante, l.DataLancamento })
            .HasDatabaseName("idx_lancamentos_comerciante_data");

        builder.HasIndex(l => l.DataLancamento)
            .HasDatabaseName("idx_lancamentos_data");

        builder.HasIndex(l => l.Status)
            .HasDatabaseName("idx_lancamentos_status");

        // Ignorar propriedade de eventos de domínio (não persistir)
        builder.Ignore(l => l.EventosDominio);
    }
}
