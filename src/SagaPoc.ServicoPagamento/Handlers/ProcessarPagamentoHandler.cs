using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.Common.Mensagens.Comandos;
using SagaPoc.Common.Mensagens.Respostas;
using SagaPoc.Infrastructure.Core;
using SagaPoc.ServicoPagamento.Servicos;

namespace SagaPoc.ServicoPagamento.Handlers;

/// <summary>
/// Handler responsável por processar pagamentos.
/// Recebe comando ProcessarPagamento e responde com PagamentoProcessado.
/// </summary>
public class ProcessarPagamentoHandler : IHandleMessages<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IRegistroCancelamentos _cancelamentos;
    private readonly IBus _bus;
    private readonly ILogger<ProcessarPagamentoHandler> _logger;

    public ProcessarPagamentoHandler(
        IServicoPagamento servico,
        IRegistroCancelamentos cancelamentos,
        IBus bus,
        ILogger<ProcessarPagamentoHandler> logger)
    {
        _servico = servico;
        _cancelamentos = cancelamentos;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(ProcessarPagamento mensagem)
    {
        // ============ TIMEOUT POLICY ============
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Timeout de 10s

        _logger.LogInformation(
            "Recebido comando ProcessarPagamento. CorrelacaoId: {CorrelacaoId}, " +
            "ClienteId: {ClienteId}, Valor: R$ {Valor:F2}, FormaPagamento: {FormaPagamento}",
            mensagem.CorrelacaoId,
            mensagem.ClienteId,
            mensagem.ValorTotal,
            mensagem.FormaPagamento
        );

        // A SAGA desistiu do pedido (prazo) antes de a cobrança chegar aqui: não cobrar.
        if (_cancelamentos.EstaCancelado(mensagem.CorrelacaoId))
        {
            _logger.LogWarning(
                "Pedido cancelado antes da cobrança; pagamento recusado. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );
            await _bus.Reply(new PagamentoProcessado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: null,
                MotivoFalha: "Pedido cancelado antes da cobrança"
            ));
            return;
        }

        try
        {
            // Executar processamento do pagamento
            var resultado = await _servico.ProcessarAsync(
                mensagem.ClienteId,
                mensagem.ValorTotal,
                mensagem.FormaPagamento,
                cts.Token
            );

            if (resultado.EhSucesso)
            {
                // Registrar antes de conferir o cancelamento (o estorno faz o inverso): se o
                // cancelamento chegou durante a cobrança, um dos dois lados desfaz a cobrança.
                _cancelamentos.RegistrarRecurso(mensagem.CorrelacaoId, resultado.Valor.TransacaoId);

                if (_cancelamentos.EstaCancelado(mensagem.CorrelacaoId))
                {
                    _logger.LogWarning(
                        "Pedido cancelado durante a cobrança; estornando. CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
                        mensagem.CorrelacaoId,
                        resultado.Valor.TransacaoId
                    );
                    var estorno = await _servico.EstornarAsync(resultado.Valor.TransacaoId);
                    if (!estorno.EhSucesso)
                    {
                        _logger.LogError(
                            "Estorno da cobrança atrasada falhou; exige intervenção manual. CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}, Erro: {Erro}",
                            mensagem.CorrelacaoId,
                            resultado.Valor.TransacaoId,
                            estorno.Erro.Mensagem
                        );
                    }

                    await _bus.Reply(new PagamentoProcessado(
                        CorrelacaoId: mensagem.CorrelacaoId,
                        Sucesso: false,
                        TransacaoId: resultado.Valor.TransacaoId,
                        MotivoFalha: "Pedido cancelado durante a cobrança; valor estornado"
                    ));
                    return;
                }
            }

            // Preparar resposta baseada no resultado
            var resposta = resultado.Match(
                sucesso: dados => new PagamentoProcessado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Sucesso: true,
                    TransacaoId: dados.TransacaoId,
                    MotivoFalha: null
                ),
                falha: erro => new PagamentoProcessado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Sucesso: false,
                    TransacaoId: null,
                    MotivoFalha: erro.Mensagem
                )
            );

            // Enviar resposta usando Rebus
            await _bus.Reply(resposta);

            _logger.LogInformation(
                "Resposta enviada. CorrelacaoId: {CorrelacaoId}, Sucesso: {Sucesso}, " +
                "TransacaoId: {TransacaoId}",
                mensagem.CorrelacaoId,
                resposta.Sucesso,
                resposta.TransacaoId
            );
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogError(
                "[TIMEOUT] Timeout ao processar pagamento. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta de falha por timeout
            await _bus.Reply(new PagamentoProcessado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Sucesso: false,
                TransacaoId: null,
                MotivoFalha: "Timeout ao processar pagamento"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao processar ProcessarPagamento. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Sem resposta aqui: o Rebus descarta o que o handler enviou quando ele falha.
            // A mensagem é repetida e, esgotadas as tentativas, vai para a fila error;
            // a SAGA não fica presa porque o prazo do pedido dispara a compensação.
            throw;
        }
    }
}
