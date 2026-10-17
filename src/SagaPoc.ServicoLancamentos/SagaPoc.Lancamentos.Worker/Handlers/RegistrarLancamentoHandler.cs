using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.Lancamentos.Worker.Handlers;

public class RegistrarLancamentoHandler : IHandleMessages<RegistrarLancamento>
{
    private readonly ILancamentoRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBus _bus;
    private readonly ILogger<RegistrarLancamentoHandler> _logger;

    public RegistrarLancamentoHandler(
        ILancamentoRepository repository,
        IUnitOfWork unitOfWork,
        IBus bus,
        ILogger<RegistrarLancamentoHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
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

        var resultadoCriacao = Lancamento.Criar(
            comando.Tipo,
            comando.Valor,
            comando.DataLancamento,
            comando.Descricao,
            comando.Comerciante,
            comando.Categoria);

        if (!resultadoCriacao.EhSucesso)
        {
            _logger.LogWarning(
                "Falha ao criar lancamento: {Erro}",
                resultadoCriacao.Erro.Mensagem);
            return;
        }

        var lancamento = resultadoCriacao.Valor;

        // Confirmado em memória antes de gravar: o lançamento entra no banco já
        // confirmado, num único INSERT, e a transação pode ser repetida sem
        // confirmar duas vezes.
        var resultadoConfirmacao = lancamento.Confirmar();
        if (!resultadoConfirmacao.EhSucesso)
        {
            throw new InvalidOperationException(
                $"Lançamento {lancamento.Id} não pôde ser confirmado: {resultadoConfirmacao.Erro.Mensagem}");
        }

        try
        {
            await _unitOfWork.ExecutarEmTransacaoAsync(async () =>
            {
                var resultadoPersistencia = await _repository.AdicionarAsync(lancamento);
                if (!resultadoPersistencia.EhSucesso)
                {
                    // Lança para o Rebus repetir a mensagem e, esgotadas as
                    // tentativas, levá-la à fila de erro em vez de perdê-la.
                    throw new InvalidOperationException(resultadoPersistencia.Erro.Mensagem);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao gravar lancamento {LancamentoId}. CorrelationId: {CorrelationId}",
                lancamento.Id,
                comando.CorrelationId);
            throw;
        }

        // O Rebus só envia as mensagens publicadas aqui depois que o handler termina.
        foreach (var evento in lancamento.EventosDominio)
        {
            _logger.LogInformation("Publicando evento: {EventoTipo}", evento.GetType().Name);
            await _bus.Publish(evento);
        }

        _logger.LogInformation(
            "Lancamento {LancamentoId} registrado e confirmado com sucesso",
            lancamento.Id);
    }
}
