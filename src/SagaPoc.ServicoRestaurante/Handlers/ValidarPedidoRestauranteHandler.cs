using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.Infrastructure.Core;
using SagaPoc.ServicoRestaurante.Servicos;

namespace SagaPoc.ServicoRestaurante.Handlers;

/// <summary>
/// Handler responsável por validar pedidos de restaurante.
/// Recebe comando ValidarPedidoRestaurante e responde com PedidoRestauranteValidado.
/// </summary>
public class ValidarPedidoRestauranteHandler : IHandleMessages<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly IRegistroCancelamentos _cancelamentos;
    private readonly IBus _bus;
    private readonly ILogger<ValidarPedidoRestauranteHandler> _logger;

    public ValidarPedidoRestauranteHandler(
        IServicoRestaurante servico,
        IRegistroCancelamentos cancelamentos,
        IBus bus,
        ILogger<ValidarPedidoRestauranteHandler> logger)
    {
        _servico = servico;
        _cancelamentos = cancelamentos;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(ValidarPedidoRestaurante mensagem)
    {
        _logger.LogInformation(
            "Recebido comando ValidarPedidoRestaurante. CorrelacaoId: {CorrelacaoId}, " +
            "RestauranteId: {RestauranteId}, QuantidadeItens: {QuantidadeItens}",
            mensagem.CorrelacaoId,
            mensagem.RestauranteId,
            mensagem.Itens.Count
        );

        // A SAGA desistiu do pedido (prazo) antes de a validação chegar aqui: não aceitar.
        if (_cancelamentos.EstaCancelado(mensagem.CorrelacaoId))
        {
            _logger.LogWarning(
                "Pedido cancelado antes da validação; pedido recusado. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );
            await _bus.Reply(new PedidoRestauranteValidado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Valido: false,
                ValorTotal: 0,
                TempoPreparoMinutos: 0,
                PedidoId: null,
                MotivoRejeicao: "Pedido cancelado antes da validação"
            ));
            return;
        }

        try
        {
            // Executar validação do pedido
            var resultado = await _servico.ValidarPedidoAsync(
                mensagem.RestauranteId,
                mensagem.Itens
            );

            if (resultado.EhSucesso)
            {
                // Registrar antes de conferir o cancelamento (o cancelamento faz o inverso).
                _cancelamentos.RegistrarRecurso(mensagem.CorrelacaoId, resultado.Valor.PedidoId.ToString());

                if (_cancelamentos.EstaCancelado(mensagem.CorrelacaoId))
                {
                    _logger.LogWarning(
                        "Pedido cancelado durante a validação; cancelando no restaurante. CorrelacaoId: {CorrelacaoId}, PedidoId: {PedidoId}",
                        mensagem.CorrelacaoId,
                        resultado.Valor.PedidoId
                    );
                    await _servico.CancelarPedidoAsync(mensagem.RestauranteId, resultado.Valor.PedidoId);

                    await _bus.Reply(new PedidoRestauranteValidado(
                        CorrelacaoId: mensagem.CorrelacaoId,
                        Valido: false,
                        ValorTotal: 0,
                        TempoPreparoMinutos: 0,
                        PedidoId: null,
                        MotivoRejeicao: "Pedido cancelado durante a validação"
                    ));
                    return;
                }
            }

            // Usar Match() para tratar sucesso/falha de forma elegante (Result Pattern)
            resultado.Match(
                sucesso: dados =>
                {
                    _logger.LogInformation(
                        "[Restaurante] Pedido {CorrelacaoId} validado com sucesso - " +
                        "Valor: R$ {Valor:F2}, Tempo: {Tempo}min, PedidoId: {PedidoId}",
                        mensagem.CorrelacaoId,
                        dados.ValorTotal,
                        dados.TempoPreparoMinutos,
                        dados.PedidoId
                    );
                },
                falha: erro =>
                {
                    _logger.LogWarning(
                        "[Restaurante] Pedido {CorrelacaoId} rejeitado - " +
                        "TipoErro: {TipoErro}, Codigo: {Codigo}, Motivo: {Motivo}",
                        mensagem.CorrelacaoId,
                        erro.Tipo,
                        erro.Codigo,
                        erro.Mensagem
                    );
                }
            );

            // Preparar resposta baseada no resultado
            var resposta = resultado.Match(
                sucesso: dados => new PedidoRestauranteValidado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Valido: true,
                    ValorTotal: dados.ValorTotal,
                    TempoPreparoMinutos: dados.TempoPreparoMinutos,
                    PedidoId: dados.PedidoId,
                    MotivoRejeicao: null
                ),
                falha: erro => new PedidoRestauranteValidado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Valido: false,
                    ValorTotal: 0,
                    TempoPreparoMinutos: 0,
                    PedidoId: null,
                    MotivoRejeicao: $"[{erro.Codigo}] {erro.Mensagem}"
                )
            );

            // Enviar resposta de volta para o orquestrador
            await _bus.Reply(resposta);

            _logger.LogInformation(
                "Resposta enviada. CorrelacaoId: {CorrelacaoId}, Valido: {Valido}",
                mensagem.CorrelacaoId,
                resposta.Valido
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao processar ValidarPedidoRestaurante. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Sem resposta aqui: o Rebus descarta o que o handler enviou quando ele falha.
            // A mensagem é repetida e, esgotadas as tentativas, vai para a fila error;
            // a SAGA não fica presa porque o prazo do pedido dispara a compensação.
            throw;
        }
    }
}
