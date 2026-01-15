namespace SagaPoc.FluxoCaixa.Api.DTOs;

public class LancamentoResponse
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime DataLancamento { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Comerciante { get; set; } = string.Empty;
    public string? Categoria { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}
