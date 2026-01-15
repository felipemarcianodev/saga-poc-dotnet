using Microsoft.Extensions.Logging;
using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Consolidado.Servicos;
using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Consolidado.Handlers;

public class LancamentoDebitoRegistradoHandler
    : IHandleMessages<LancamentoDebitoRegistrado>
{
    private readonly IConsolidadoDiarioRepository _repository;
    private readonly ICacheService _cache;
    private readonly ILogger<LancamentoDebitoRegistradoHandler> _logger;

    public LancamentoDebitoRegistradoHandler(
        IConsolidadoDiarioRepository repository,
        ICacheService cache,
        ILogger<LancamentoDebitoRegistradoHandler> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task Handle(LancamentoDebitoRegistrado evento)
    {
        _logger.LogInformation(
            "Processando débito: {LancamentoId} - Valor: {Valor} - Data: {Data}",
            evento.LancamentoId,
            evento.Valor,
            evento.DataLancamento);

        // Obter ou criar consolidado
        var resultadoConsolidado = await _repository.ObterOuCriarAsync(
            evento.DataLancamento,
            evento.Comerciante);

        if (resultadoConsolidado.EhFalha)
        {
            _logger.LogError(
                "Falha ao obter consolidado: {Erro}",
                resultadoConsolidado.Erro.Mensagem);
            throw new InvalidOperationException(resultadoConsolidado.Erro.Mensagem);
        }

        var consolidado = resultadoConsolidado.Valor;

        // Aplicar débito
        consolidado.AplicarDebito(evento.Valor);

        // Salvar
        var resultadoSalvar = await _repository.SalvarAsync(consolidado);

        if (resultadoSalvar.EhFalha)
        {
            _logger.LogError("Falha ao salvar consolidado: {Erro}",
                resultadoSalvar.Erro.Mensagem);
            throw new InvalidOperationException(resultadoSalvar.Erro.Mensagem);
        }

        // Invalidar cache
        await _cache.RemoveAsync(
            $"consolidado:{evento.Comerciante}:{evento.DataLancamento:yyyy-MM-dd}");

        _logger.LogInformation(
            "Consolidado atualizado com débito. Saldo atual: {Saldo}",
            consolidado.SaldoDiario);
    }
}
