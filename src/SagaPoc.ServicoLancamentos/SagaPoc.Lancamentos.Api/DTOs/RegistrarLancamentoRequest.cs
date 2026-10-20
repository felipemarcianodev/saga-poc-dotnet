using SagaPoc.FluxoCaixa.Domain.ValueObjects;
using System.ComponentModel.DataAnnotations;

namespace SagaPoc.Lancamentos.Api.DTOs;

/// <summary>
/// Requisição para registrar um novo lançamento no fluxo de caixa
/// </summary>
public class RegistrarLancamentoRequest
{
    /// <summary>
    /// Tipo do lançamento (1 = Débito, 2 = Crédito)
    /// </summary>
    [Required(ErrorMessage = "O tipo do lançamento é obrigatório")]
    [EnumDataType(typeof(EnumTipoLancamento), ErrorMessage = "O tipo deve ser 1 (Débito) ou 2 (Crédito)")]
    public EnumTipoLancamento? Tipo { get; set; }

    /// <summary>
    /// Valor do lançamento em reais
    /// </summary>
    [Required(ErrorMessage = "O valor é obrigatório")]
    [Range(typeof(decimal), "0.01", "1000000000", ErrorMessage = "O valor deve estar entre 0,01 e 1.000.000.000,00")]
    public decimal? Valor { get; set; }

    /// <summary>
    /// Data do lançamento (opcional, padrão = data atual)
    /// </summary>
    public DateTime? DataLancamento { get; set; }

    /// <summary>
    /// Descrição detalhada do lançamento
    /// </summary>
    [Required(ErrorMessage = "A descrição é obrigatória")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "A descrição deve ter entre 3 e 500 caracteres")]
    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// Identificador do comerciante
    /// </summary>
    [Required(ErrorMessage = "O comerciante é obrigatório")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O identificador do comerciante deve ter entre 3 e 100 caracteres")]
    public string Comerciante { get; set; } = string.Empty;

    /// <summary>
    /// Categoria do lançamento (opcional)
    /// </summary>
    [StringLength(100, ErrorMessage = "A categoria deve ter no máximo 100 caracteres")]
    public string? Categoria { get; set; }
}
