namespace SagaPoc.Infrastructure.Core;

/// <summary>
/// Registro, por pedido (CorrelacaoId), do recurso que o serviço criou e de que o pedido foi cancelado.
/// Permite compensar sem o identificador do recurso (a resposta com ele pode nunca ter chegado à SAGA)
/// e recusar ou desfazer uma operação que termina depois do cancelamento.
/// </summary>
public interface IRegistroCancelamentos
{
    /// <summary>
    /// Marca o pedido como cancelado. Operação que chegar depois para o mesmo pedido deve ser recusada.
    /// </summary>
    void MarcarCancelado(Guid correlacaoId);

    /// <summary>
    /// Indica se o pedido já foi cancelado.
    /// </summary>
    bool EstaCancelado(Guid correlacaoId);

    /// <summary>
    /// Guarda o recurso criado para o pedido (transação, entregador, pedido no restaurante).
    /// </summary>
    void RegistrarRecurso(Guid correlacaoId, string recursoId);

    /// <summary>
    /// Recurso criado para o pedido, ou null se nenhum foi criado.
    /// </summary>
    string? ObterRecurso(Guid correlacaoId);
}
