using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Application.Services;

/// <summary>
/// Forma do consolidado gravada no Redis. O agregado não vai direto para o cache:
/// com construtor privado e setters privados, o System.Text.Json não consegue
/// desserializá-lo, e toda leitura do Redis caía no banco.
/// </summary>
public sealed record ConsolidadoDiarioCache(
    Guid Id,
    DateTime Data,
    string Comerciante,
    decimal TotalCreditos,
    decimal TotalDebitos,
    int QuantidadeCreditos,
    int QuantidadeDebitos,
    DateTime UltimaAtualizacao)
{
    public static ConsolidadoDiarioCache De(ConsolidadoDiario consolidado) => new(
        consolidado.Id,
        consolidado.Data,
        consolidado.Comerciante,
        consolidado.TotalCreditos,
        consolidado.TotalDebitos,
        consolidado.QuantidadeCreditos,
        consolidado.QuantidadeDebitos,
        consolidado.UltimaAtualizacao);

    public ConsolidadoDiario ParaAgregado() => ConsolidadoDiario.Reconstituir(
        Id,
        Data,
        Comerciante,
        TotalCreditos,
        TotalDebitos,
        QuantidadeCreditos,
        QuantidadeDebitos,
        UltimaAtualizacao);
}
