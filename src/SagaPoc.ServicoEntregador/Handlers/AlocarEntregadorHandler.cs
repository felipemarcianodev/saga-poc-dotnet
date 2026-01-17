using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.ServicoEntregador.Servicos;

namespace SagaPoc.ServicoEntregador.Handlers;

/// <summary>
/// Handler responsável por alocar entregadores.
/// Recebe comando AlocarEntregador e responde com EntregadorAlocado.
/// </summary>
public class AlocarEntregadorHandler : IHandleMessages<AlocarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly IBus _bus;
    private readonly ILogger<AlocarEntregadorHandler> _logger;

    public AlocarEntregadorHandler(
        IServicoEntregador servico,
        IBus bus,
        ILogger<AlocarEntregadorHandler> logger)
    {
        _servico = servico;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(AlocarEntregador mensagem)
    {
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

            // Enviar resposta usando Rebus
            await _bus.Reply(resposta);

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
            await _bus.Reply(new EntregadorAlocado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Alocado: false,
                EntregadorId: null,
                TempoEstimadoMinutos: 0,
                MotivoFalha: "Erro interno ao alocar entregador"
            ));

            throw; // Re-throw para Rebus lidar com retry policy
        }
    }
}
