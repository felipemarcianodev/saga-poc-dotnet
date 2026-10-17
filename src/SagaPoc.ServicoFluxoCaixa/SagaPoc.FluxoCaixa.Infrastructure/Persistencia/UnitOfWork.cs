using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Infrastructure.Persistencia;

public class UnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context;

    public UnitOfWork(TContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task ExecutarEmTransacaoAsync(Func<Task> operacao, CancellationToken ct = default)
    {
        // O DbContext usa EnableRetryOnFailure, que recusa transação aberta fora
        // da estratégia de execução. A estratégia repete o bloco inteiro.
        var estrategia = _context.Database.CreateExecutionStrategy();

        await estrategia.ExecuteAsync(async () =>
        {
            // Descarta o que uma tentativa anterior deixou rastreado.
            _context.ChangeTracker.Clear();

            await using var transacao = await _context.Database.BeginTransactionAsync(ct);
            await operacao();
            await _context.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        });
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
