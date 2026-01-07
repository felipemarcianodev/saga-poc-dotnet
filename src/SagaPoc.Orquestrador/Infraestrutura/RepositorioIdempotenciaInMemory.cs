using System.Collections.Concurrent;

namespace SagaPoc.Orquestrador.Infraestrutura;

/// <summary>
/// Implementação em memória do repositório de idempotência.
/// Adequado para POC e testes. Para produção, usar Redis ou banco de dados.
/// </summary>
public class RepositorioIdempotenciaInMemory : IRepositorioIdempotencia
{
    private readonly ConcurrentDictionary<string, (DateTime DataProcessamento, object Dados)> _cache = new();
    private readonly TimeSpan _tempoExpiracao = TimeSpan.FromHours(24);

    /// <summary>
    /// Verifica se uma operação já foi processada.
    /// Remove automaticamente entradas expiradas.
    /// </summary>
    public Task<bool> JaProcessadoAsync(string chave)
    {
        if (_cache.TryGetValue(chave, out var entrada))
        {
            // Verificar se não expirou
            if (DateTime.UtcNow - entrada.DataProcessamento < _tempoExpiracao)
            {
                return Task.FromResult(true);
            }

            // Remover entrada expirada
            _cache.TryRemove(chave, out _);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Marca uma operação como processada, armazenando timestamp e dados.
    /// </summary>
    public Task MarcarProcessadoAsync(string chave, object dados)
    {
        _cache[chave] = (DateTime.UtcNow, dados);
        return Task.CompletedTask;
    }
}
