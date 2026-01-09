using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rebus.PostgreSql.Sagas;

namespace SagaPoc.Orquestrador.Persistence;

/// <summary>
/// Extensões para inicialização e gestão do banco de dados.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Garante que o banco de dados e as tabelas necessárias sejam criados.
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<SagaDbContext>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        try
        {
            // Criar o banco de dados se não existir
            var connectionString = configuration.GetConnectionString("SagaDatabase")
                ?? throw new InvalidOperationException("Connection string 'SagaDatabase' não encontrada.");

            logger.LogInformation("Verificando banco de dados...");

            // Criar DbContext e aplicar migrations
            var context = services.GetRequiredService<SagaDbContext>();
            await context.Database.MigrateAsync();

            logger.LogInformation("Banco de dados verificado com sucesso.");

            // Criar tabelas do Rebus para SAGAs
            logger.LogInformation("Criando tabelas do Rebus...");
            await CreateRebusSagaTables(connectionString, logger);

            logger.LogInformation("Tabelas do Rebus criadas com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao inicializar o banco de dados");
            throw;
        }
    }

    /// <summary>
    /// Cria as tabelas necessárias para armazenar SAGAs no PostgreSQL.
    /// </summary>
    private static async Task CreateRebusSagaTables(string connectionString, ILogger logger)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Criar tabela de dados da SAGA
            var createDataTableSql = @"
                CREATE TABLE IF NOT EXISTS ""PedidoSagas"" (
                    ""Id"" UUID PRIMARY KEY,
                    ""Revision"" INTEGER NOT NULL,
                    ""Data"" JSONB NOT NULL
                );
            ";

            await using (var command = new NpgsqlCommand(createDataTableSql, connection))
            {
                await command.ExecuteNonQueryAsync();
                logger.LogInformation("Tabela 'PedidoSagas' criada/verificada.");
            }

            // Criar tabela de índices da SAGA
            var createIndexTableSql = @"
                CREATE TABLE IF NOT EXISTS ""PedidoSagasIndex"" (
                    ""SagaId"" UUID NOT NULL,
                    ""Key"" VARCHAR(200) NOT NULL,
                    ""Value"" VARCHAR(1024) NOT NULL,
                    ""SagaType"" VARCHAR(200) NOT NULL,
                    PRIMARY KEY (""Key"", ""Value"", ""SagaType"")
                );
            ";

            await using (var command = new NpgsqlCommand(createIndexTableSql, connection))
            {
                await command.ExecuteNonQueryAsync();
                logger.LogInformation("Tabela 'PedidoSagasIndex' criada/verificada.");
            }

            // Criar índices para melhor performance
            var createIndexSql = @"
                CREATE INDEX IF NOT EXISTS ""IX_PedidoSagasIndex_SagaId""
                ON ""PedidoSagasIndex"" (""SagaId"");
            ";

            await using (var command = new NpgsqlCommand(createIndexSql, connection))
            {
                await command.ExecuteNonQueryAsync();
                logger.LogInformation("Índices criados/verificados.");
            }

            logger.LogInformation("Todas as tabelas do Rebus foram criadas com sucesso.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao criar tabelas do Rebus");
            throw;
        }
    }
}
