using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;

namespace SagaPoc.Orquestrador.Consumers;

/// <summary>
/// Consumer para processar mensagens da Dead Letter Queue (DLQ).
/// Lida com mensagens que falharam após todas as tentativas de retry.
/// </summary>
public class DeadLetterQueueConsumer : IConsumer<Fault<IniciarPedido>>
{
    private readonly ILogger<DeadLetterQueueConsumer> _logger;

    public DeadLetterQueueConsumer(ILogger<DeadLetterQueueConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<Fault<IniciarPedido>> context)
    {
        var mensagemOriginal = context.Message.Message;
        var excecoes = context.Message.Exceptions;

        _logger.LogError(
            "[DLQ] Mensagem {MessageId} movida para DLQ após {TentativasRetry} tentativas - " +
            "CorrelacaoId: {CorrelacaoId}, Erros: {Erros}",
            context.MessageId,
            excecoes.Length,
            mensagemOriginal.CorrelacaoId,
            string.Join("; ", excecoes.Select(e => e.Message))
        );

        // Registrar detalhes completos da falha
        foreach (var excecao in excecoes)
        {
            _logger.LogError(
                "[DLQ] Exceção na tentativa - Tipo: {ExceptionType}, Mensagem: {Message}, StackTrace: {StackTrace}",
                excecao.ExceptionType,
                excecao.Message,
                excecao.StackTrace
            );
        }

        // Em produção, aqui você poderia:
        // 1. Armazenar em banco de dados para análise posterior
        // await _repositorio.SalvarMensagemFalhadaAsync(mensagemOriginal, excecoes);

        // 2. Enviar alerta para equipe de operações
        // await _servicoNotificacao.EnviarAlertaAsync($"Pedido {mensagemOriginal.CorrelacaoId} falhou após todas as tentativas");

        // 3. Criar ticket no sistema de suporte
        // await _servicoTicket.CriarTicketAsync($"DLQ - Pedido {mensagemOriginal.CorrelacaoId}", detalhes);

        await Task.CompletedTask;
    }
}
