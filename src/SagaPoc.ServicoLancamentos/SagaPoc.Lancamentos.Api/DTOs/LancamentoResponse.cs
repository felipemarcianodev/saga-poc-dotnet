namespace SagaPoc.Lancamentos.Api.DTOs;

/// <summary>
/// Resposta com dados de um lançamento
/// </summary>
public class LancamentoResponse
{
    /// <summary>
    /// Identificador único do lançamento
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Tipo do lançamento
    /// </summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>
    /// Valor do lançamento
    /// </summary>
    public decimal Valor { get; set; }

    /// <summary>
    /// Data em que o lançamento foi realizado
    /// </summary>
    public DateTime DataLancamento { get; set; }

    /// <summary>
    /// Descrição do lançamento
    /// </summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// Identificador do comerciante
    /// </summary>
    public string Comerciante { get; set; } = string.Empty;

    /// <summary>
    /// Categoria do lançamento
    /// </summary>
    public string? Categoria { get; set; }

    /// <summary>
    /// Status atual do lançamento
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora de criação do registro
    /// </summary>
    public DateTime CriadoEm { get; set; }
}
