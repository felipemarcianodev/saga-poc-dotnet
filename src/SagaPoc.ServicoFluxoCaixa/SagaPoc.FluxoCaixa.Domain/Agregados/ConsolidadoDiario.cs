using SagaPoc.Common.DDD;
using SagaPoc.FluxoCaixa.Domain.ValueObjects;

namespace SagaPoc.FluxoCaixa.Domain.Agregados;

public class ConsolidadoDiario : AggregateRoot
{
    public Guid Id { get; private set; }
    public DateTime Data { get; private set; }
    public string Comerciante { get; private set; } = string.Empty;
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal SaldoDiario => TotalCreditos - TotalDebitos;
    public int QuantidadeCreditos { get; private set; }
    public int QuantidadeDebitos { get; private set; }
    public int QuantidadeTotalLancamentos => QuantidadeCreditos + QuantidadeDebitos;
    public DateTime UltimaAtualizacao { get; private set; }

    // Para EF Core
    private ConsolidadoDiario() { }

    public static ConsolidadoDiario Criar(DateTime data, string comerciante)
    {
        return new ConsolidadoDiario
        {
            Id = Guid.NewGuid(),
            Data = data.Date,
            Comerciante = comerciante,
            TotalCreditos = 0,
            TotalDebitos = 0,
            QuantidadeCreditos = 0,
            QuantidadeDebitos = 0,
            UltimaAtualizacao = DateTime.UtcNow
        };
    }

    public void AplicarCredito(decimal valor)
    {
        if (valor <= 0)
            throw new InvalidOperationException("Valor de crédito deve ser positivo");

        TotalCreditos += valor;
        QuantidadeCreditos++;
        UltimaAtualizacao = DateTime.UtcNow;
    }

    public void AplicarDebito(decimal valor)
    {
        if (valor <= 0)
            throw new InvalidOperationException("Valor de débito deve ser positivo");

        TotalDebitos += valor;
        QuantidadeDebitos++;
        UltimaAtualizacao = DateTime.UtcNow;
    }

    public void Recalcular(IEnumerable<Lancamento> lancamentos)
    {
        TotalCreditos = 0;
        TotalDebitos = 0;
        QuantidadeCreditos = 0;
        QuantidadeDebitos = 0;

        foreach (var lancamento in lancamentos)
        {
            if (lancamento.Tipo == EnumTipoLancamento.Credito)
                AplicarCredito(lancamento.Valor);
            else
                AplicarDebito(lancamento.Valor);
        }

        UltimaAtualizacao = DateTime.UtcNow;
    }
}
