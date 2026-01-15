using SagaPoc.FluxoCaixa.Domain.ValueObjects;

namespace SagaPoc.FluxoCaixa.Api.DTOs;

public class RegistrarLancamentoRequest
{
    public EnumTipoLancamento Tipo { get; set; }
    public decimal Valor { get; set; }
    public DateTime? DataLancamento { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Comerciante { get; set; } = string.Empty;
    public string? Categoria { get; set; }
}
