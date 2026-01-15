using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Infrastructure.Repositorios;

public class ConsolidadoDiarioRepository : IConsolidadoDiarioRepository
{
    private readonly ConsolidadoDbContext _context;
    private readonly ILogger<ConsolidadoDiarioRepository> _logger;

    public ConsolidadoDiarioRepository(
        ConsolidadoDbContext context,
        ILogger<ConsolidadoDiarioRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Resultado<ConsolidadoDiario>> ObterOuCriarAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default)
    {
        try
        {
            var consolidado = await _context.Consolidados
                .FirstOrDefaultAsync(
                    c => c.Data == data.Date && c.Comerciante == comerciante,
                    ct);

            if (consolidado is null)
            {
                consolidado = ConsolidadoDiario.Criar(data.Date, comerciante);
                await _context.Consolidados.AddAsync(consolidado, ct);
                await _context.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Consolidado criado para {Data} - Comerciante: {Comerciante}",
                    data.Date,
                    comerciante);
            }

            return Resultado<ConsolidadoDiario>.Sucesso(consolidado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter ou criar consolidado");
            return Resultado<ConsolidadoDiario>.Falha(Erro.Tecnico(
                "Consolidado.ErroObtencao",
                "Erro ao obter consolidado diário"));
        }
    }

    public async Task<Resultado<ConsolidadoDiario>> ObterPorDataAsync(
        DateTime data,
        string comerciante,
        CancellationToken ct = default)
    {
        try
        {
            var consolidado = await _context.Consolidados
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.Data == data.Date && c.Comerciante == comerciante,
                    ct);

            if (consolidado is null)
            {
                return Resultado<ConsolidadoDiario>.Falha(Erro.NaoEncontrado(
                    $"Consolidado não encontrado para {data:yyyy-MM-dd}",
                    "Consolidado.NaoEncontrado"));
            }

            return Resultado<ConsolidadoDiario>.Sucesso(consolidado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar consolidado por data");
            return Resultado<ConsolidadoDiario>.Falha(Erro.Tecnico(
                "Consolidado.ErroBusca",
                "Erro ao buscar consolidado"));
        }
    }

    public async Task<Resultado<IEnumerable<ConsolidadoDiario>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default)
    {
        try
        {
            var consolidados = await _context.Consolidados
                .AsNoTracking()
                .Where(c => c.Comerciante == comerciante
                         && c.Data >= inicio.Date
                         && c.Data <= fim.Date)
                .OrderBy(c => c.Data)
                .ToListAsync(ct);

            return Resultado<IEnumerable<ConsolidadoDiario>>.Sucesso(consolidados);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar consolidados por período");
            return Resultado<IEnumerable<ConsolidadoDiario>>.Falha(Erro.Tecnico(
                "Consolidado.ErroBusca",
                "Erro ao buscar consolidados por período"));
        }
    }

    public async Task<Resultado<Unit>> SalvarAsync(
        ConsolidadoDiario consolidado,
        CancellationToken ct = default)
    {
        try
        {
            _context.Consolidados.Update(consolidado);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Consolidado atualizado: {Data} - Saldo: {Saldo}",
                consolidado.Data,
                consolidado.SaldoDiario);

            return Resultado.Sucesso();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro ao salvar consolidado");
            return Resultado<Unit>.Falha(Erro.Tecnico(
                "Consolidado.ErroSalvar",
                "Erro ao salvar consolidado"));
        }
    }
}
