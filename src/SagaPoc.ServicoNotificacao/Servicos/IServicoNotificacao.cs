using SagaPoc.Common.Modelos;
using SagaPoc.Common.ResultPattern;

namespace SagaPoc.ServicoNotificacao.Servicos;

/// <summary>
/// Interface para o serviço de envio de notificações.
/// </summary>
public interface IServicoNotificacao
{
    /// <summary>
    /// Envia uma notificação para um cliente.
    /// </summary>
    /// <param name="clienteId">Identificador do cliente.</param>
    /// <param name="mensagem">Mensagem a ser enviada.</param>
    /// <param name="tipo">Tipo da notificação.</param>
    /// <returns>Resultado indicando sucesso ou falha do envio.</returns>
    Task<Resultado<Unit>> EnviarAsync(
        string clienteId,
        string mensagem,
        TipoNotificacao tipo
    );
}
