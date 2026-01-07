using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoNotificacao.Servicos;

namespace SagaPoc.ServicoNotificacao.Consumers;

/// <summary>
/// Consumer responsável por enviar notificações aos clientes.
/// Recebe comando NotificarCliente e responde com NotificacaoEnviada.
/// </summary>
public class NotificarClienteConsumer : IConsumer<NotificarCliente>
{
    private readonly IServicoNotificacao _servico;
    private readonly ILogger<NotificarClienteConsumer> _logger;

    public NotificarClienteConsumer(
        IServicoNotificacao servico,
        ILogger<NotificarClienteConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificarCliente> context)
    {
        var mensagem = context.Message;

        _logger.LogInformation(
            "Recebido comando NotificarCliente. CorrelacaoId: {CorrelacaoId}, " +
            "ClienteId: {ClienteId}, Tipo: {Tipo}",
            mensagem.CorrelacaoId,
            mensagem.ClienteId,
            mensagem.Tipo
        );

        try
        {
            // Executar envio da notificação
            var resultado = await _servico.EnviarAsync(
                mensagem.ClienteId,
                mensagem.Mensagem,
                mensagem.Tipo
            );

            // Preparar resposta baseada no resultado
            // IMPORTANTE: Mesmo que a notificação falhe, não vamos travar a SAGA
            // A SAGA é considerada completa mesmo sem notificação
            var resposta = new NotificacaoEnviada(
                CorrelacaoId: mensagem.CorrelacaoId,
                Enviada: resultado.EhSucesso
            );

            // Enviar resposta
            await context.RespondAsync(resposta);

            if (resultado.EhSucesso)
            {
                _logger.LogInformation(
                    "Notificação enviada com sucesso. CorrelacaoId: {CorrelacaoId}, ClienteId: {ClienteId}",
                    mensagem.CorrelacaoId,
                    mensagem.ClienteId
                );
            }
            else
            {
                _logger.LogWarning(
                    "Falha ao enviar notificação (não crítico). CorrelacaoId: {CorrelacaoId}, " +
                    "ClienteId: {ClienteId}, Erro: {Erro}",
                    mensagem.CorrelacaoId,
                    mensagem.ClienteId,
                    resultado.Erro.Mensagem
                );
                // Não lançar exceção - falha em notificação não deve travar a SAGA
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao processar NotificarCliente. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta indicando falha, mas não re-throw
            // Notificações são "best effort" - não devem bloquear a SAGA
            await context.RespondAsync(new NotificacaoEnviada(
                CorrelacaoId: mensagem.CorrelacaoId,
                Enviada: false
            ));

            // Não re-throw - permitir que a SAGA continue mesmo com falha de notificação
        }
    }
}
