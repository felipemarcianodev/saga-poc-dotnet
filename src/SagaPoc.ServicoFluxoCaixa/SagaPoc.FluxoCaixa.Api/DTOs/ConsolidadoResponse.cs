namespace SagaPoc.FluxoCaixa.Api.DTOs;

public class ConsolidadoResponse
{
    public DateTime Data { get; set; }
    public string Comerciante { get; set; } = string.Empty;
    public decimal TotalCreditos { get; set; }
    public decimal TotalDebitos { get; set; }
    public decimal SaldoDiario { get; set; }
    public int QuantidadeCreditos { get; set; }
    public int QuantidadeDebitos { get; set; }
    public int QuantidadeTotalLancamentos { get; set; }
    public DateTime UltimaAtualizacao { get; set; }
}
