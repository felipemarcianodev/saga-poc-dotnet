using SagaPoc.Common.Modelos;
using SagaPoc.Common.ResultPattern;

namespace SagaPoc.ServicoNotificacao.Servicos;

/// <summary>
/// Implementação do serviço de envio de notificações.
/// NOTA: Esta é uma implementação simulada para fins de POC.
/// Em produção, isso integraria com serviços como SendGrid, Twilio, Firebase, etc.
/// </summary>
public class ServicoNotificacao : IServicoNotificacao
{
    private readonly ILogger<ServicoNotificacao> _logger;

    // Simulação de histórico de notificações enviadas
    private static readonly List<(string ClienteId, string Mensagem, TipoNotificacao Tipo, DateTime Data)> Historico = new();

    public ServicoNotificacao(ILogger<ServicoNotificacao> logger)
    {
        _logger = logger;
    }

    public async Task<Resultado<Unit>> EnviarAsync(
        string clienteId,
        string mensagem,
        TipoNotificacao tipo)
    {
        _logger.LogInformation(
            "Enviando notificação. ClienteId: {ClienteId}, Tipo: {Tipo}, Mensagem: {Mensagem}",
            clienteId,
            tipo,
            mensagem
        );

        // Simulação de delay de processamento (envio via email/SMS/push)
        await Task.Delay(Random.Shared.Next(100, 400));

        // CENÁRIO 1: Cliente com notificações desativadas
        if (clienteId == "CLI_SEM_NOTIFICACAO")
        {
            _logger.LogWarning(
                "Cliente {ClienteId} tem notificações desativadas",
                clienteId
            );
            return Resultado.Falha(
                Erro.Negocio("NOTIFICACOES_DESATIVADAS", "Cliente desativou o recebimento de notificações")
            );
        }

        // CENÁRIO 2: Email/telefone inválido
        if (clienteId == "CLI_INVALIDO")
        {
            _logger.LogWarning(
                "Cliente {ClienteId} não possui dados de contato válidos",
                clienteId
            );
            return Resultado.Falha(
                Erro.Validacao("CONTATO_INVALIDO", "Cliente não possui email ou telefone válido cadastrado")
            );
        }

        // CENÁRIO 3: Falha temporária no serviço de notificação (simulação)
        if (Random.Shared.Next(100) < 5) // 5% de chance de falha
        {
            _logger.LogError(
                "Falha temporária ao enviar notificação para {ClienteId}",
                clienteId
            );
            return Resultado.Falha(
                Erro.Tecnico("FALHA_ENVIO", "Falha temporária no serviço de notificação. Tente novamente.")
            );
        }

        // CENÁRIO 4: Notificação enviada com sucesso
        Historico.Add((clienteId, mensagem, tipo, DateTime.UtcNow));

        // Simular diferentes canais baseado no tipo
        var canal = tipo switch
        {
            TipoNotificacao.PedidoConfirmado => "Email + Push",
            TipoNotificacao.PedidoCancelado => "Email + SMS",
            TipoNotificacao.EntregadorAlocado => "Push",
            TipoNotificacao.PedidoEmPreparacao => "Push",
            TipoNotificacao.PedidoSaiuParaEntrega => "Push + SMS",
            TipoNotificacao.PedidoEntregue => "Email + Push",
            _ => "Push"
        };

        _logger.LogInformation(
            "Notificação enviada com sucesso. ClienteId: {ClienteId}, Tipo: {Tipo}, Canal: {Canal}",
            clienteId,
            tipo,
            canal
        );

        return Resultado.Sucesso();
    }
}
