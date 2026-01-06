namespace SagaPoc.Shared.Mensagens.Respostas;

/// <summary>
/// Resposta do envio de notificação ao cliente.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Enviada">Indica se a notificação foi enviada com sucesso.</param>
public record NotificacaoEnviada(
    Guid CorrelacaoId,
    bool Enviada
);
