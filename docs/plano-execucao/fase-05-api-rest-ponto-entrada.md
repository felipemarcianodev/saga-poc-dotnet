# FASE 5: API REST (Ponto de Entrada)


#### 3.5.1 Objetivos
- Criar API REST para iniciar SAGA
- Endpoint para consultar status do pedido
- Documentação OpenAPI/Swagger

#### 3.5.2 Entregas

##### 1. **Controller de Pedidos**
```csharp
[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CriarPedido(
        [FromBody] CriarPedidoRequest request)
    {
        var correlacaoId = Guid.NewGuid();

        await _publishEndpoint.Publish(new IniciarPedido(
            correlacaoId,
            request.ClienteId,
            request.RestauranteId,
            request.Itens,
            request.EnderecoEntrega,
            request.FormaPagamento
        ));

        return Accepted(new {
            PedidoId = correlacaoId,
            Mensagem = "Pedido recebido e está sendo processado.",
            Status = "Pendente"
        });
    }

    [HttpGet("{pedidoId}/status")]
    public async Task<IActionResult> ConsultarStatus(Guid pedidoId)
    {
        // Consultar estado da saga
        // Retornar status atual do pedido
        return Ok(new {
            PedidoId = pedidoId,
            Status = "EmProcessamento",
            UltimaAtualizacao = DateTime.UtcNow
        });
    }
}
```

##### 2. **DTOs**
```csharp
public record CriarPedidoRequest(
    string ClienteId,
    string RestauranteId,
    List<ItemPedido> Itens,
    string EnderecoEntrega,
    string FormaPagamento
);
```

#### 3.5.3 Critérios de Aceitação
- [ ] API aceita requisições e retorna 202 Accepted
- [ ] Swagger configurado e funcional
- [ ] Correlação ID retornado para rastreamento

---

