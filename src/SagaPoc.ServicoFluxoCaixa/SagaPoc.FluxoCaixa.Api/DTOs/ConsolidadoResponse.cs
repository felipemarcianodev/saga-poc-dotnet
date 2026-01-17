using Swashbuckle.AspNetCore.Annotations;

namespace SagaPoc.FluxoCaixa.Api.DTOs;

/// <summary>
/// Resposta com o consolidado diário do fluxo de caixa
/// </summary>
[SwaggerSchema("Dados consolidados do fluxo de caixa diário")]
public class ConsolidadoResponse
{
    /// <summary>
    /// Data de referência do consolidado
    /// </summary>
    [SwaggerSchema("Data do consolidado", Description = "Data de referência para o consolidado diário")]
    public DateTime Data { get; set; } = DateTime.Today;

    /// <summary>
    /// Identificador do comerciante
    /// </summary>
    [SwaggerSchema("Comerciante", Description = "Código ou identificador do comerciante")]
    public string Comerciante { get; set; } = "COM001";

    /// <summary>
    /// Total de créditos do dia
    /// </summary>
    [SwaggerSchema("Total de créditos", Description = "Soma de todos os lançamentos de crédito do dia em reais (BRL)")]
    public decimal TotalCreditos { get; set; } = 500.00m;

    /// <summary>
    /// Total de débitos do dia
    /// </summary>
    [SwaggerSchema("Total de débitos", Description = "Soma de todos os lançamentos de débito do dia em reais (BRL)")]
    public decimal TotalDebitos { get; set; } = 150.00m;

    /// <summary>
    /// Saldo líquido do dia (créditos - débitos)
    /// </summary>
    [SwaggerSchema("Saldo diário", Description = "Saldo líquido do dia (Total de Créditos - Total de Débitos)")]
    public decimal SaldoDiario { get; set; } = 350.00m;

    /// <summary>
    /// Quantidade de lançamentos de crédito
    /// </summary>
    [SwaggerSchema("Quantidade de créditos", Description = "Número de lançamentos de crédito registrados no dia")]
    public int QuantidadeCreditos { get; set; } = 5;

    /// <summary>
    /// Quantidade de lançamentos de débito
    /// </summary>
    [SwaggerSchema("Quantidade de débitos", Description = "Número de lançamentos de débito registrados no dia")]
    public int QuantidadeDebitos { get; set; } = 3;

    /// <summary>
    /// Quantidade total de lançamentos
    /// </summary>
    [SwaggerSchema("Total de lançamentos", Description = "Número total de lançamentos (créditos + débitos)")]
    public int QuantidadeTotalLancamentos { get; set; } = 8;

    /// <summary>
    /// Data e hora da última atualização do consolidado
    /// </summary>
    [SwaggerSchema("Última atualização", Description = "Data e hora da última atualização deste consolidado")]
    public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
}
