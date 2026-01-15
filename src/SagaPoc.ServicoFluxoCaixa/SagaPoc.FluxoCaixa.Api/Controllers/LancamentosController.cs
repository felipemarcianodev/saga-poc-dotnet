using Microsoft.AspNetCore.Mvc;
using Rebus.Bus;
using SagaPoc.FluxoCaixa.Api.DTOs;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LancamentosController : ControllerBase
{
    private readonly IBus _bus;
    private readonly ILancamentoRepository _repository;
    private readonly ILogger<LancamentosController> _logger;

    public LancamentosController(
        IBus bus,
        ILancamentoRepository repository,
        ILogger<LancamentosController> logger)
    {
        _bus = bus;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Registra um novo lançamento (débito ou crédito)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarLancamentoRequest request,
        CancellationToken ct)
    {
        var comando = new RegistrarLancamento(
            Guid.NewGuid(), // CorrelationId
            request.Tipo,
            request.Valor,
            request.DataLancamento ?? DateTime.Today,
            request.Descricao,
            request.Comerciante,
            request.Categoria);

        await _bus.Send(comando);

        _logger.LogInformation(
            "Comando RegistrarLancamento enviado. CorrelationId: {CorrelationId}",
            comando.CorrelationId);

        return Accepted(new
        {
            correlationId = comando.CorrelationId,
            mensagem = "Lançamento enviado para processamento"
        });
    }

    /// <summary>
    /// Consulta um lançamento por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var resultado = await _repository.ObterPorIdAsync(id, ct);

        if (resultado.EhFalha)
            return NotFound(new { erro = resultado.Erro.Mensagem });

        var lancamento = resultado.Valor;

        return Ok(new LancamentoResponse
        {
            Id = lancamento.Id,
            Tipo = lancamento.Tipo.ToString(),
            Valor = lancamento.Valor,
            DataLancamento = lancamento.DataLancamento,
            Descricao = lancamento.Descricao,
            Comerciante = lancamento.Comerciante,
            Categoria = lancamento.Categoria,
            Status = lancamento.Status.ToString(),
            CriadoEm = lancamento.CriadoEm
        });
    }

    /// <summary>
    /// Lista lançamentos por período
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] string comerciante,
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken ct)
    {
        var dataInicio = inicio ?? DateTime.Today.AddDays(-30);
        var dataFim = fim ?? DateTime.Today;

        var resultado = await _repository.ObterPorPeriodoAsync(
            comerciante,
            dataInicio,
            dataFim,
            ct);

        if (resultado.EhFalha)
            return BadRequest(new { erro = resultado.Erro.Mensagem });

        var lancamentos = resultado.Valor.Select(l => new LancamentoResponse
        {
            Id = l.Id,
            Tipo = l.Tipo.ToString(),
            Valor = l.Valor,
            DataLancamento = l.DataLancamento,
            Descricao = l.Descricao,
            Comerciante = l.Comerciante,
            Categoria = l.Categoria,
            Status = l.Status.ToString(),
            CriadoEm = l.CriadoEm
        });

        return Ok(lancamentos);
    }
}
