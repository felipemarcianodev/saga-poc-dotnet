using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Domain.Respostas;

namespace SagaPoc.FluxoCaixa.Lancamentos.Handlers;

public class RegistrarLancamentoHandler : IHandleMessages<RegistrarLancamento>
{
    private readonly ILancamentoRepository _repository;
    private readonly IBus _bus;
    private readonly ILogger<RegistrarLancamentoHandler> _logger;

    public RegistrarLancamentoHandler(
        ILancamentoRepository repository,
        IBus bus,
        ILogger<RegistrarLancamentoHandler> logger)
    {
        _repository = repository;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(RegistrarLancamento comando)
    {
        _logger.LogInformation(
            "Processando comando RegistrarLancamento. Tipo: {Tipo}, Valor: {Valor}, Comerciante: {Comerciante}",
            comando.Tipo,
            comando.Valor,
            comando.Comerciante);

        // Criar agregado usando factory method
        var resultadoCriacao = Lancamento.Criar(
            comando.Tipo,
            comando.Valor,
            comando.DataLancamento,
            comando.Descricao,
            comando.Comerciante,
            comando.Categoria);

        if (resultadoCriacao.EhFalha)
        {
            _logger.LogWarning(
                "Falha ao criar lançamento: {Erro}",
                resultadoCriacao.Erro.Mensagem);

            // Arquitetura fire-and-forget: não enviamos reply
            // await _bus.Reply(new LancamentoRejeitado(
            //     comando.CorrelationId,
            //     resultadoCriacao.Erro.Codigo,
            //     resultadoCriacao.Erro.Mensagem));

            return;
        }

        var lancamento = resultadoCriacao.Valor;

        // Persistir
        var resultadoPersistencia = await _repository.AdicionarAsync(lancamento);

        if (resultadoPersistencia.EhFalha)
        {
            _logger.LogError(
                "Falha ao persistir lançamento: {Erro}",
                resultadoPersistencia.Erro.Mensagem);

            // Arquitetura fire-and-forget: não enviamos reply
            // await _bus.Reply(new LancamentoRejeitado(
            //     comando.CorrelationId,
            //     resultadoPersistencia.Erro.Codigo,
            //     resultadoPersistencia.Erro.Mensagem));

            return;
        }

        // Confirmar lançamento
        var resultadoConfirmacao = lancamento.Confirmar();
        if (resultadoConfirmacao.EhSucesso)
        {
            await _repository.AtualizarAsync(lancamento);
        }

        // Publicar eventos de domínio
        _logger.LogInformation("Publicando {Count} eventos de domínio", lancamento.EventosDominio.Count);
        foreach (var evento in lancamento.EventosDominio)
        {
            _logger.LogInformation("Publicando evento: {EventoTipo}", evento.GetType().Name);
            await _bus.Publish(evento);
        }

        // Arquitetura fire-and-forget: não enviamos reply
        // await _bus.Reply(new LancamentoRegistradoComSucesso(
        //     comando.CorrelationId,
        //     lancamento.Id,
        //     lancamento.Tipo,
        //     lancamento.Valor,
        //     lancamento.DataLancamento));

        _logger.LogInformation(
            "Lançamento {LancamentoId} registrado e confirmado com sucesso",
            lancamento.Id);
    }
}
