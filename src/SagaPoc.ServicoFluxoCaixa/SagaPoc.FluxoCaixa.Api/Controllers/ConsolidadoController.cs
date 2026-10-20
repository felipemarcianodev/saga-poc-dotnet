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

        if (!resultado.EhSucesso)
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterPorPeriodo(
        string comerciante,
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken ct)
    {
        // Sem as datas, o binding usava DateTime.MinValue e a consulta devolvia lista vazia com 200.
        if (inicio is null || fim is null)
            return BadRequest(new { erro = "Informe inicio e fim (formato yyyy-MM-dd)" });

        if (inicio > fim)
            return BadRequest(new { erro = "A data de início deve ser anterior ou igual à data de fim" });

        var resultado = await _consolidadoService.ObterPorPeriodoAsync(
            comerciante, inicio.Value, fim.Value, ct);

        if (!resultado.EhSucesso)
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
