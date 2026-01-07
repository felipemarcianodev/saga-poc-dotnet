namespace SagaPoc.Orquestrador.Infraestrutura;

/// <summary>
/// Repositório para controle de idempotência nas operações de compensação.
/// Garante que operações de compensação não sejam executadas múltiplas vezes.
/// </summary>
public interface IRepositorioIdempotencia
{
    /// <summary>
    /// Verifica se uma operação já foi processada.
    /// </summary>
    /// <param name="chave">Chave única que identifica a operação.</param>
    /// <returns>True se já foi processada, False caso contrário.</returns>
    Task<bool> JaProcessadoAsync(string chave);

    /// <summary>
    /// Marca uma operação como processada.
    /// </summary>
    /// <param name="chave">Chave única que identifica a operação.</param>
    /// <param name="dados">Dados adicionais sobre o processamento.</param>
    Task MarcarProcessadoAsync(string chave, object dados);
}
