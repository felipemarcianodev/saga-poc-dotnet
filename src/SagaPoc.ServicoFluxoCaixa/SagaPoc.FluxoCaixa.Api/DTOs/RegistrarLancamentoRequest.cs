using SagaPoc.FluxoCaixa.Domain.ValueObjects;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace SagaPoc.FluxoCaixa.Api.DTOs;

/// <summary>
/// Requisição para registrar um novo lançamento no fluxo de caixa
/// </summary>
[SwaggerSchema("Dados necessários para registrar um lançamento (débito ou crédito)")]
public class RegistrarLancamentoRequest
{
    /// <summary>
    /// Tipo do lançamento (1 = Débito, 2 = Crédito)
    /// </summary>
    [Required(ErrorMessage = "O tipo do lançamento é obrigatório")]
    [SwaggerSchema("Tipo do lançamento", Description = "1 = Débito (saída de caixa), 2 = Crédito (entrada de caixa)")]
    public EnumTipoLancamento Tipo { get; set; } = EnumTipoLancamento.Credito;

    /// <summary>
    /// Valor do lançamento em reais
    /// </summary>
    [Required(ErrorMessage = "O valor é obrigatório")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O valor deve ser maior que zero")]
    [SwaggerSchema("Valor do lançamento", Description = "Valor em reais (BRL), exemplo: 150.00")]
    public decimal Valor { get; set; } = 150.00m;

    /// <summary>
    /// Data do lançamento (opcional, padrão = data atual)
    /// </summary>
    [SwaggerSchema("Data do lançamento", Description = "Data em que o lançamento ocorreu. Se não informada, será considerada a data atual.")]
    public DateTime? DataLancamento { get; set; } = DateTime.Today;

    /// <summary>
    /// Descrição detalhada do lançamento
    /// </summary>
    [Required(ErrorMessage = "A descrição é obrigatória")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "A descrição deve ter entre 3 e 500 caracteres")]
    [SwaggerSchema("Descrição do lançamento", Description = "Descrição detalhada do lançamento, ex: 'Venda de produto X'")]
    public string Descricao { get; set; } = "Venda de produto X";

    /// <summary>
    /// Identificador do comerciante
    /// </summary>
    [Required(ErrorMessage = "O comerciante é obrigatório")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O identificador do comerciante deve ter entre 3 e 100 caracteres")]
    [SwaggerSchema("Identificador do comerciante", Description = "Código ou identificador único do comerciante, ex: 'COM001'")]
    public string Comerciante { get; set; } = "COM001";

    /// <summary>
    /// Categoria do lançamento (opcional)
    /// </summary>
    [StringLength(100, ErrorMessage = "A categoria deve ter no máximo 100 caracteres")]
    [SwaggerSchema("Categoria do lançamento", Description = "Categoria para classificação do lançamento, ex: 'Vendas', 'Despesas Operacionais'")]
    public string? Categoria { get; set; } = "Vendas";
}
