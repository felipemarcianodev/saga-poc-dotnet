using System.Collections.Concurrent;

namespace SagaPoc.Infrastructure.Core;

/// <summary>
/// Implementação em memória do registro de cancelamentos.
/// Adequado para POC: o registro se perde quando o serviço reinicia e não vale entre instâncias.
/// </summary>
public class RegistroCancelamentosInMemory : IRegistroCancelamentos
{
    private readonly ConcurrentDictionary<Guid, DateTime> _cancelados = new();
    private readonly ConcurrentDictionary<Guid, string> _recursos = new();

    public void MarcarCancelado(Guid correlacaoId) => _cancelados.TryAdd(correlacaoId, DateTime.UtcNow);

    public bool EstaCancelado(Guid correlacaoId) => _cancelados.ContainsKey(correlacaoId);

    public void RegistrarRecurso(Guid correlacaoId, string recursoId) => _recursos[correlacaoId] = recursoId;

    public string? ObterRecurso(Guid correlacaoId) =>
        _recursos.TryGetValue(correlacaoId, out var recursoId) ? recursoId : null;
}
