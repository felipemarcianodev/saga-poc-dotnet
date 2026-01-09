using Microsoft.EntityFrameworkCore;
using SagaPoc.Orquestrador.Sagas;

namespace SagaPoc.Orquestrador.Persistence;

/// <summary>
/// DbContext para persistência do estado das SAGAs usando Entity Framework Core.
/// Garante que o estado da SAGA sobreviva a reinicializações do orquestrador.
/// </summary>
public class SagaDbContext : DbContext
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Conjunto de dados para as SAGAs de pedidos.
    /// </summary>
    public DbSet<PedidoSagaData> PedidoSagas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração da entidade PedidoSagaData
        modelBuilder.Entity<PedidoSagaData>(entity =>
        {
            // Nome da tabela
            entity.ToTable("PedidoSagas");

            // Chave primária
            entity.HasKey(e => e.Id);

            // Propriedades obrigatórias
            entity.Property(e => e.Id)
                .IsRequired()
                .HasComment("ID único da instância da SAGA");

            entity.Property(e => e.Revision)
                .IsRequired()
                .IsConcurrencyToken()
                .HasComment("Número de revisão para controle de concorrência otimista");

            entity.Property(e => e.EstadoAtual)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("Estado atual da SAGA");

            entity.Property(e => e.ClienteId)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("ID do cliente que fez o pedido");

            entity.Property(e => e.RestauranteId)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("ID do restaurante onde o pedido foi feito");

            entity.Property(e => e.ValorTotal)
                .HasPrecision(18, 2)
                .HasComment("Valor total do pedido");

            entity.Property(e => e.EnderecoEntrega)
                .IsRequired()
                .HasMaxLength(500)
                .HasComment("Endereço para entrega do pedido");

            entity.Property(e => e.FormaPagamento)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("Forma de pagamento utilizada");

            // Propriedades opcionais de compensação
            entity.Property(e => e.TransacaoId)
                .HasMaxLength(100)
                .HasComment("ID da transação de pagamento (necessário para estorno)");

            entity.Property(e => e.EntregadorId)
                .HasMaxLength(100)
                .HasComment("ID do entregador alocado (necessário para liberação)");

            entity.Property(e => e.PedidoRestauranteId)
                .HasComment("ID do pedido no sistema do restaurante");

            entity.Property(e => e.EmCompensacao)
                .HasComment("Indica se o pedido está em processo de compensação");

            entity.Property(e => e.DataInicioCompensacao)
                .HasComment("Timestamp de início da compensação");

            entity.Property(e => e.DataConclusaoCompensacao)
                .HasComment("Timestamp de conclusão da compensação");

            // Listas serializadas como JSON
            entity.Property(e => e.PassosCompensados)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList()
                )
                .HasComment("Lista de passos compensados com sucesso");

            entity.Property(e => e.ErrosCompensacao)
                .HasConversion(
                    v => string.Join(';', v),
                    v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList()
                )
                .HasComment("Erros ocorridos durante a compensação");

            // Flags de controle
            entity.Property(e => e.RestauranteValidado)
                .HasComment("Indica se a validação do restaurante foi executada");

            entity.Property(e => e.PagamentoProcessado)
                .HasComment("Indica se o pagamento foi processado");

            entity.Property(e => e.EntregadorAlocado)
                .HasComment("Indica se o entregador foi alocado");

            entity.Property(e => e.TentativasCompensacao)
                .HasComment("Contador de tentativas de compensação");

            // Timestamps
            entity.Property(e => e.DataInicio)
                .IsRequired()
                .HasComment("Data e hora de início do processamento da SAGA");

            entity.Property(e => e.DataConclusao)
                .HasComment("Data e hora de conclusão da SAGA");

            // Métricas
            entity.Property(e => e.TempoPreparoMinutos)
                .HasComment("Tempo estimado de preparo do pedido em minutos");

            entity.Property(e => e.TempoEntregaMinutos)
                .HasComment("Tempo estimado de entrega em minutos");

            entity.Property(e => e.TaxaEntrega)
                .HasPrecision(18, 2)
                .HasComment("Taxa de entrega cobrada");

            // Mensagens de erro
            entity.Property(e => e.MensagemErro)
                .HasMaxLength(1000)
                .HasComment("Mensagem de erro em caso de falha");

            entity.Property(e => e.MotivoRejeicao)
                .HasMaxLength(1000)
                .HasComment("Motivo da rejeição/cancelamento do pedido");

            // Índices para consultas otimizadas
            entity.HasIndex(e => e.ClienteId)
                .HasDatabaseName("IX_PedidoSagas_ClienteId");

            entity.HasIndex(e => e.RestauranteId)
                .HasDatabaseName("IX_PedidoSagas_RestauranteId");

            entity.HasIndex(e => e.EstadoAtual)
                .HasDatabaseName("IX_PedidoSagas_EstadoAtual");

            entity.HasIndex(e => e.DataInicio)
                .HasDatabaseName("IX_PedidoSagas_DataInicio");
        });
    }
}
