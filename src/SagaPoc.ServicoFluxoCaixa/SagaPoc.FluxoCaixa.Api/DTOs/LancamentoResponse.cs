using Swashbuckle.AspNetCore.Annotations;

namespace SagaPoc.FluxoCaixa.Api.DTOs;

/// <summary>
/// Resposta com dados de um lançamento
/// </summary>
[SwaggerSchema("Dados completos de um lançamento registrado")]
public class LancamentoResponse
{
    /// <summary>
    /// Identificador único do lançamento
    /// </summary>
    [SwaggerSchema("ID do lançamento", Description = "Identificador único (GUID) do lançamento")]
    public Guid Id { get; set; } = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    /// <summary>
    /// Tipo do lançamento
    /// </summary>
    [SwaggerSchema("Tipo do lançamento", Description = "Tipo do lançamento: 'Debito' ou 'Credito'")]
    public string Tipo { get; set; } = "Credito";

    /// <summary>
    /// Valor do lançamento
    /// </summary>
    [SwaggerSchema("Valor", Description = "Valor em reais (BRL)")]
    public decimal Valor { get; set; } = 150.00m;

    /// <summary>
    /// Data em que o lançamento foi realizado
    /// </summary>
    [SwaggerSchema("Data do lançamento", Description = "Data em que o lançamento foi realizado")]
    public DateTime DataLancamento { get; set; } = DateTime.Today;

    /// <summary>
    /// Descrição do lançamento
    /// </summary>
    [SwaggerSchema("Descrição", Description = "Descrição detalhada do lançamento")]
    public string Descricao { get; set; } = "Venda de produto X";

    /// <summary>
    /// Identificador do comerciante
    /// </summary>
    [SwaggerSchema("Comerciante", Description = "Código ou identificador do comerciante")]
    public string Comerciante { get; set; } = "COM001";

    /// <summary>
    /// Categoria do lançamento
    /// </summary>
    [SwaggerSchema("Categoria", Description = "Categoria para classificação do lançamento")]
    public string? Categoria { get; set; } = "Vendas";

    /// <summary>
    /// Status atual do lançamento
    /// </summary>
    [SwaggerSchema("Status", Description = "Status do lançamento: 'Pendente', 'Confirmado' ou 'Cancelado'")]
    public string Status { get; set; } = "Confirmado";

    /// <summary>
    /// Data e hora de criação do registro
    /// </summary>
    [SwaggerSchema("Criado em", Description = "Data e hora em que o lançamento foi criado")]
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
