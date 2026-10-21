using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.Infrastructure.Core;
using SagaPoc.ServicoEntregador.Servicos;

namespace SagaPoc.ServicoEntregador.Handlers;

/// <summary>
/// Handler responsável por alocar entregadores.
/// Recebe comando AlocarEntregador e responde com EntregadorAlocado.
/// </summary>
public class AlocarEntregadorHandler : IHandleMessages<AlocarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly IRegistroCancelamentos _cancelamentos;
    private readonly IBus _bus;
    private readonly ILogger<AlocarEntregadorHandler> _logger;

    public AlocarEntregadorHandler(
        IServicoEntregador servico,
        IRegistroCancelamentos cancelamentos,
        IBus bus,
        ILogger<AlocarEntregadorHandler> logger)
    {
        _servico = servico;
        _cancelamentos = cancelamentos;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(AlocarEntregador mensagem)
    {
        _logger.LogInformation(
            "Recebido comando AlocarEntregador. CorrelacaoId: {CorrelacaoId}, " +
            "RestauranteId: {RestauranteId}, Endereco: {Endereco}, Taxa: R$ {Taxa:F2}",
            mensagem.CorrelacaoId,
            mensagem.RestauranteId,
            mensagem.EnderecoEntrega,
            mensagem.TaxaEntrega
        );

        // A SAGA desistiu do pedido (prazo) antes de a alocação chegar aqui: não alocar.
        if (_cancelamentos.EstaCancelado(mensagem.CorrelacaoId))
        {
            _logger.LogWarning(
                "Pedido cancelado antes da alocação; entregador não alocado. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );
            await _bus.Reply(new EntregadorAlocado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Alocado: false,
                EntregadorId: null,
                TempoEstimadoMinutos: 0,
                MotivoFalha: "Pedido cancelado antes da alocação"
            ));
            return;
        }

        try
        {
            // Executar alocação do entregador
            var resultado = await _servico.AlocarAsync(
                mensagem.RestauranteId,
                mensagem.EnderecoEntrega,
                mensagem.TaxaEntrega
            );

            if (resultado.EhSucesso)
            {
                // Registrar antes de conferir o cancelamento (a liberação faz o inverso).
                _cancelamentos.RegistrarRecurso(mensagem.CorrelacaoId, resultado.Valor.EntregadorId);

                if (_cancelamentos.EstaCancelado(mensagem.CorrelacaoId))
                {
                    _logger.LogWarning(
                        "Pedido cancelado durante a alocação; liberando o entregador. CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
                        mensagem.CorrelacaoId,
                        resultado.Valor.EntregadorId
                    );
                    await _servico.LiberarAsync(resultado.Valor.EntregadorId);

                    await _bus.Reply(new EntregadorAlocado(
                        CorrelacaoId: mensagem.CorrelacaoId,
                        Alocado: false,
                        EntregadorId: null,
                        TempoEstimadoMinutos: 0,
                        MotivoFalha: "Pedido cancelado durante a alocação"
                    ));
                    return;
                }
            }

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

            // Sem resposta aqui: o Rebus descarta o que o handler enviou quando ele falha.
            // A mensagem é repetida e, esgotadas as tentativas, vai para a fila error;
            // a SAGA não fica presa porque o prazo do pedido dispara a compensação.
            throw;
        }
    }
}
