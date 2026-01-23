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

        if (resultadoCriacao.EhFalha)
        {
            _logger.LogWarning(
                "Falha ao criar lancamento: {Erro}",
                resultadoCriacao.Erro.Mensagem);
            return;
        }

        var lancamento = resultadoCriacao.Valor;

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var resultadoPersistencia = await _repository.AdicionarAsync(lancamento);

            if (resultadoPersistencia.EhFalha)
            {
                _logger.LogError(
                    "Falha ao persistir lancamento: {Erro}",
                    resultadoPersistencia.Erro.Mensagem);
                await _unitOfWork.RollbackAsync();
                return;
            }

            var resultadoConfirmacao = lancamento.Confirmar();
            if (resultadoConfirmacao.EhSucesso)
            {
                await _repository.AtualizarAsync(lancamento);
            }

            await _unitOfWork.CommitAsync();

            _logger.LogInformation("Publicando {Count} eventos de dominio", lancamento.EventosDominio.Count);
            foreach (var evento in lancamento.EventosDominio)
            {
                _logger.LogInformation("Publicando evento: {EventoTipo}", evento.GetType().Name);
                await _bus.Publish(evento);
            }

            _logger.LogInformation(
                "Lancamento {LancamentoId} registrado e confirmado com sucesso",
                lancamento.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar lancamento. Realizando rollback.");
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
