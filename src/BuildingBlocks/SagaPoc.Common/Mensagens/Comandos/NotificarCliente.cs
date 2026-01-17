using SagaPoc.Common.Modelos;

namespace SagaPoc.Common.Mensagens.Comandos;

/// <summary>
/// Comando para enviar uma notificação ao cliente.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="ClienteId">Identificador do cliente.</param>
/// <param name="Mensagem">Mensagem a ser enviada.</param>
/// <param name="Tipo">Tipo da notificação.</param>
public record NotificarCliente(
    Guid CorrelacaoId,
    string ClienteId,
    string Mensagem,
    TipoNotificacao Tipo
);
