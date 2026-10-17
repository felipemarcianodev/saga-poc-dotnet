namespace SagaPoc.FluxoCaixa.Domain.Repositorios;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Executa a operação numa transação e grava as alterações ao final.
    /// Em falha transitória do banco, a operação inteira é repetida; por isso
    /// ela deve ler o estado de novo a cada execução, sem depender de variáveis
    /// preenchidas numa tentativa anterior.
    /// </summary>
    Task ExecutarEmTransacaoAsync(Func<Task> operacao, CancellationToken ct = default);
}
