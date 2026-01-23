using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Application.Services;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Consolidado.Handlers;

public class LancamentoDebitoRegistradoHandler
    : IHandleMessages<LancamentoDebitoRegistrado>
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly ILogger<LancamentoDebitoRegistradoHandler> _logger;

    public LancamentoDebitoRegistradoHandler(
        IConsolidadoDiarioRepository repository,
        IUnitOfWork unitOfWork,
        ICacheService cache,
        ILogger<LancamentoDebitoRegistradoHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(LancamentoDebitoRegistrado evento)
    {
        _logger.LogInformation(
            "Processando debito: {LancamentoId} - Valor: {Valor} - Data: {Data}",
            evento.LancamentoId,
            evento.Valor,
            evento.DataLancamento);

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var resultadoConsolidado = await _repository.ObterAsync(
                evento.DataLancamento,
                evento.Comerciante);

            if (resultadoConsolidado.EhFalha)
            {
                _logger.LogError(
                    "Falha ao obter consolidado: {Erro}",
                    resultadoConsolidado.Erro.Mensagem);
                await _unitOfWork.RollbackAsync();
                throw new InvalidOperationException(resultadoConsolidado.Erro.Mensagem);
            }

            var consolidado = resultadoConsolidado.Valor;

            if (consolidado is null)
            {
                var dataUtc = DateTime.SpecifyKind(evento.DataLancamento.Date, DateTimeKind.Utc);
                consolidado = ConsolidadoDiario.Criar(dataUtc, evento.Comerciante);
                await _repository.AdicionarAsync(consolidado);

                _logger.LogInformation(
                    "Consolidado criado para {Data} - Comerciante: {Comerciante}",
                    dataUtc,
                    evento.Comerciante);
            }

            consolidado.AplicarDebito(evento.Valor);
            await _repository.AtualizarAsync(consolidado);

            await _unitOfWork.CommitAsync();

            await _cache.RemoveAsync(
                $"consolidado:{evento.Comerciante}:{evento.DataLancamento:yyyy-MM-dd}");

            _logger.LogInformation(
                "Consolidado atualizado com debito. Saldo atual: {Saldo}",
                consolidado.SaldoDiario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar debito. Realizando rollback.");
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
