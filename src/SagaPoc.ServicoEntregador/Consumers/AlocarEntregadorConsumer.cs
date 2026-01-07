using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoEntregador.Servicos;

namespace SagaPoc.ServicoEntregador.Consumers;

/// <summary>
/// Consumer responsável por alocar entregadores.
/// Recebe comando AlocarEntregador e responde com EntregadorAlocado.
/// </summary>
public class AlocarEntregadorConsumer : IConsumer<AlocarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly ILogger<AlocarEntregadorConsumer> _logger;

    public AlocarEntregadorConsumer(
        IServicoEntregador servico,
        ILogger<AlocarEntregadorConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AlocarEntregador> context)
    {
        var mensagem = context.Message;

        _logger.LogInformation(
            "Recebido comando AlocarEntregador. CorrelacaoId: {CorrelacaoId}, " +
            "RestauranteId: {RestauranteId}, Endereco: {Endereco}, Taxa: {Taxa:C}",
            mensagem.CorrelacaoId,
            mensagem.RestauranteId,
            mensagem.EnderecoEntrega,
            mensagem.TaxaEntrega
        );

        try
        {
            // Executar alocação do entregador
            var resultado = await _servico.AlocarAsync(
                mensagem.RestauranteId,
                mensagem.EnderecoEntrega,
                mensagem.TaxaEntrega
            );

            // Preparar resposta baseada no resultado
            var resposta = resultado.Match(
                sucesso: dados => new EntregadorAlocado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Alocado: true,
                    EntregadorId: dados.EntregadorId,
                    TempoEstimadoMinutos: dados.TempoEstimadoMinutos,
                    MotivoFalha: null
                ),
                falha: erro => new EntregadorAlocado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Alocado: false,
                    EntregadorId: null,
                    TempoEstimadoMinutos: 0,
                    MotivoFalha: erro.Mensagem
                )
            );

            // Enviar resposta
            await context.RespondAsync(resposta);

            _logger.LogInformation(
                "Resposta enviada. CorrelacaoId: {CorrelacaoId}, Alocado: {Alocado}, " +
                "EntregadorId: {EntregadorId}",
                mensagem.CorrelacaoId,
                resposta.Alocado,
                resposta.EntregadorId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao processar AlocarEntregador. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta de falha em caso de exceção inesperada
            await context.RespondAsync(new EntregadorAlocado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Alocado: false,
                EntregadorId: null,
                TempoEstimadoMinutos: 0,
                MotivoFalha: "Erro interno ao alocar entregador"
            ));

            throw; // Re-throw para MassTransit lidar com retry policy
        }
    }
}
