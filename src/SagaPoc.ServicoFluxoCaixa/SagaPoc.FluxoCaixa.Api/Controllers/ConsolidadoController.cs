using Microsoft.AspNetCore.Mvc;
using SagaPoc.FluxoCaixa.Api.DTOs;
using SagaPoc.FluxoCaixa.Application.Services;

namespace SagaPoc.FluxoCaixa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsolidadoController : ControllerBase
{
    private readonly IConsolidadoAppService _consolidadoService;
    private readonly ILogger<ConsolidadoController> _logger;

    public ConsolidadoController(
        IConsolidadoAppService consolidadoService,
        ILogger<ConsolidadoController> logger)
    {
        _consolidadoService = consolidadoService;
        _logger = logger;
    }

    /// <summary>
    /// Retorna o consolidado diário para uma data específica
    /// </summary>
    [HttpGet("{comerciante}/{data:datetime}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "comerciante", "data" })]
    public async Task<IActionResult> ObterPorData(
        string comerciante,
        DateTime data,
        CancellationToken ct)
    {
        var resultado = await _consolidadoService.ObterPorDataAsync(comerciante, data, ct);

        if (resultado.EhFalha)
        {
            _logger.LogWarning(
                "Consolidado nao encontrado: {Comerciante} - {Data}",
                comerciante, data);

            return NotFound(new { erro = resultado.Erro.Mensagem });
        }

        var consolidado = resultado.Valor;

        return Ok(new ConsolidadoResponse
        {
            Data = consolidado.Data,
            Comerciante = consolidado.Comerciante,
            TotalCreditos = consolidado.TotalCreditos,
            TotalDebitos = consolidado.TotalDebitos,
            SaldoDiario = consolidado.SaldoDiario,
            QuantidadeCreditos = consolidado.QuantidadeCreditos,
            QuantidadeDebitos = consolidado.QuantidadeDebitos,
            QuantidadeTotalLancamentos = consolidado.QuantidadeTotalLancamentos,
            UltimaAtualizacao = consolidado.UltimaAtualizacao
        });
    }

    /// <summary>
    /// Retorna os consolidados de um período
    /// </summary>
    [HttpGet("{comerciante}/periodo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorPeriodo(
        string comerciante,
        [FromQuery] DateTime inicio,
        [FromQuery] DateTime fim,
        CancellationToken ct)
    {
        var resultado = await _consolidadoService.ObterPorPeriodoAsync(
            comerciante, inicio, fim, ct);

        if (resultado.EhFalha)
            return BadRequest(new { erro = resultado.Erro.Mensagem });

        var consolidados = resultado.Valor.Select(c => new ConsolidadoResponse
        {
            Data = c.Data,
            Comerciante = c.Comerciante,
            TotalCreditos = c.TotalCreditos,
            TotalDebitos = c.TotalDebitos,
            SaldoDiario = c.SaldoDiario,
            QuantidadeCreditos = c.QuantidadeCreditos,
            QuantidadeDebitos = c.QuantidadeDebitos,
            QuantidadeTotalLancamentos = c.QuantidadeTotalLancamentos,
            UltimaAtualizacao = c.UltimaAtualizacao
        });

        return Ok(consolidados);
    }
}
