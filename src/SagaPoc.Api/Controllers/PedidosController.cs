using Microsoft.AspNetCore.Mvc;
using Rebus.Bus;
using SagaPoc.Api.DTOs;
using SagaPoc.Shared.Mensagens.Comandos;

namespace SagaPoc.Api.Controllers;

/// <summary>
/// Controller para gerenciar pedidos de delivery.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PedidosController : ControllerBase
{
    private readonly IBus? _bus;
    private readonly ILogger<PedidosController> _logger;

    /// <summary>
    /// Construtor do PedidosController.
    /// </summary>
    public PedidosController(
        ILogger<PedidosController> logger,
        IBus? bus = null)
    {
        _bus = bus;
        _logger = logger;
    }

    /// <summary>
    /// Cria um novo pedido e inicia a SAGA de processamento.
    /// </summary>
    /// <param name="request">Dados do pedido a ser criado.</param>
    /// <returns>Informações sobre o pedido criado.</returns>
    /// <response code="202">Pedido aceito e em processamento.</response>
    /// <response code="400">Dados inválidos fornecidos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PedidoCriadoResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CriarPedido([FromBody] CriarPedidoRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Recebendo novo pedido - Cliente: {ClienteId}, Restaurante: {RestauranteId}, Itens: {QtdItens}",
                request.ClienteId,
                request.RestauranteId,
                request.Itens.Count);

            // Gerar ID de correlação único para rastrear a SAGA
            var correlacaoId = Guid.NewGuid();

            // Enviar comando para iniciar a SAGA (se Rebus estiver configurado)
            if (_bus != null)
            {
                await _bus.Send(new IniciarPedido(
                    correlacaoId,
                    request.ClienteId,
                    request.RestauranteId,
                    request.Itens,
                    request.EnderecoEntrega,
                    request.FormaPagamento
                ));

                _logger.LogInformation(
                    "Pedido {PedidoId} enviado com sucesso para processamento",
                    correlacaoId);
            }
            else
            {
                _logger.LogWarning(
                    "Pedido {PedidoId} criado em modo DEMO - mensagem NÃO foi enviada (RabbitMQ não configurado)",
                    correlacaoId);
            }

            // Retornar 202 Accepted com informações do pedido
            var response = new PedidoCriadoResponse
            {
                PedidoId = correlacaoId,
                Mensagem = "Pedido recebido e está sendo processado. Use o PedidoId para consultar o status.",
                Status = "Pendente",
                DataRecebimento = DateTime.UtcNow
            };

            return AcceptedAtAction(
                nameof(ConsultarStatus),
                new { pedidoId = correlacaoId },
                response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar criação do pedido");
            return StatusCode(500, new
            {
                Erro = "Erro interno ao processar o pedido",
                Mensagem = "Ocorreu um erro ao processar sua solicitação. Tente novamente mais tarde."
            });
        }
    }

    /// <summary>
    /// Consulta o status atual de um pedido.
    /// </summary>
    /// <param name="pedidoId">ID de correlação do pedido.</param>
    /// <returns>Status atual do pedido.</returns>
    /// <response code="200">Status do pedido encontrado.</response>
    /// <response code="404">Pedido não encontrado.</response>
    [HttpGet("{pedidoId}/status")]
    [ProducesResponseType(typeof(StatusPedidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ConsultarStatus(Guid pedidoId)
    {
        try
        {
            _logger.LogInformation("Consultando status do pedido {PedidoId}", pedidoId);

            // TODO: Implementar consulta ao repositório da SAGA para obter estado atual
            // Por enquanto, retornar resposta mockada

            // Simulação de consulta ao estado da SAGA
            // Em produção, isso consultaria o SagaRepository (Redis, SQL, etc)

            var response = new StatusPedidoResponse
            {
                PedidoId = pedidoId,
                Status = "EmProcessamento",
                UltimaAtualizacao = DateTime.UtcNow,
                Mensagem = "O pedido está sendo processado. Aguarde a confirmação.",
                Detalhes = null // Será preenchido com dados reais da SAGA
            };

            // TODO: Quando implementar consulta real ao repositório da SAGA,
            // este método deve voltar a ser async

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status do pedido {PedidoId}", pedidoId);
            return StatusCode(500, new
            {
                Erro = "Erro interno ao consultar status",
                Mensagem = "Ocorreu um erro ao consultar o status do pedido."
            });
        }
    }

    /// <summary>
    /// Health check do controller.
    /// </summary>
    /// <returns>Status OK.</returns>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Controller = "PedidosController",
            Timestamp = DateTime.UtcNow
        });
    }
}
