using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.FluxoCaixa.Infrastructure.Persistencia;
using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Infrastructure.Repositorios;

public class LancamentoRepository : ILancamentoRepository
{
    private readonly FluxoCaixaDbContext _context;
    private readonly ILogger<LancamentoRepository> _logger;

    public LancamentoRepository(
        FluxoCaixaDbContext context,
        ILogger<LancamentoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Resultado<Lancamento>> AdicionarAsync(
        Lancamento lancamento,
        CancellationToken ct = default)
    {
        try
        {
            await _context.Lancamentos.AddAsync(lancamento, ct);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Lançamento {LancamentoId} adicionado com sucesso. Tipo: {Tipo}, Valor: {Valor}",
                lancamento.Id,
                lancamento.Tipo,
                lancamento.Valor);

            return Resultado<Lancamento>.Sucesso(lancamento);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro ao adicionar lançamento {LancamentoId}", lancamento.Id);
            return Resultado<Lancamento>.Falha(Erro.Tecnico(
                "Lancamento.ErroPersistencia",
                "Erro ao persistir o lançamento no banco de dados"));
        }
    }

    public async Task<Resultado<Lancamento>> ObterPorIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var lancamento = await _context.Lancamentos
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id, ct);

            if (lancamento is null)
            {
                return Resultado<Lancamento>.Falha(Erro.NaoEncontrado(
                    $"Lançamento {id} não foi encontrado",
                    "Lancamento.NaoEncontrado"));
            }

            return Resultado<Lancamento>.Sucesso(lancamento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar lançamento {LancamentoId}", id);
            return Resultado<Lancamento>.Falha(Erro.Tecnico(
                "Lancamento.ErroBusca",
                "Erro ao buscar o lançamento"));
        }
    }

    public async Task<Resultado<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default)
    {
        try
        {
            var lancamentos = await _context.Lancamentos
                .AsNoTracking()
                .Where(l => l.Comerciante == comerciante
                         && l.DataLancamento >= inicio.Date
                         && l.DataLancamento <= fim.Date)
                .OrderBy(l => l.DataLancamento)
                .ToListAsync(ct);

            return Resultado<IEnumerable<Lancamento>>.Sucesso(lancamentos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao buscar lançamentos por período. Comerciante: {Comerciante}, Período: {Inicio} - {Fim}",
                comerciante, inicio, fim);

            return Resultado<IEnumerable<Lancamento>>.Falha(Erro.Tecnico(
                "Lancamento.ErroBusca",
                "Erro ao buscar lançamentos por período"));
        }
    }

    public async Task<Resultado<IEnumerable<Lancamento>>> ObterPorDataAsync(
        string comerciante,
        DateTime data,
        CancellationToken ct = default)
    {
        return await ObterPorPeriodoAsync(comerciante, data.Date, data.Date, ct);
    }

    public async Task<Resultado<Unit>> AtualizarAsync(
        Lancamento lancamento,
        CancellationToken ct = default)
    {
        try
        {
            _context.Lancamentos.Update(lancamento);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Lançamento {LancamentoId} atualizado com sucesso", lancamento.Id);

            return Resultado.Sucesso();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro ao atualizar lançamento {LancamentoId}", lancamento.Id);
            return Resultado<Unit>.Falha(Erro.Tecnico(
                "Lancamento.ErroAtualizacao",
                "Erro ao atualizar o lançamento"));
        }
    }
}
