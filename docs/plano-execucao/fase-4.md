# FASE 4: Implementação dos Serviços (Consumers)


#### 3.4.1 Objetivos
- Implementar consumers em cada serviço
- Aplicar Result Pattern em toda lógica de negócio
- Simular operações (mock de banco/APIs externas)

#### 3.4.2 Entregas

##### 1. **Serviço de Restaurante**
```csharp
public class ValidarPedidoRestauranteConsumer : IConsumer<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;

    public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
    {
        var resultado = await _servico.ValidarPedidoAsync(
            context.Message.RestauranteId,
            context.Message.Itens
        );

        await context.RespondAsync(new PedidoRestauranteValidado(
            context.Message.CorrelacaoId,
            resultado.EhSucesso,
            resultado.EhSucesso ? resultado.Valor.ValorTotal : 0,
            resultado.EhSucesso ? resultado.Valor.TempoPreparoMinutos : 0,
            resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}

public interface IServicoRestaurante
{
    Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens
    );
}

public record DadosValidacaoPedido(decimal ValorTotal, int TempoPreparoMinutos);

public class ServicoRestaurante : IServicoRestaurante
{
    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        // Simular validação
        if (restauranteId == "REST_FECHADO")
            return Resultado<DadosValidacaoPedido>.Falha("Restaurante fechado no momento");

        if (itens.Any(i => i.ProdutoId == "INDISPONIVEL"))
            return Resultado<DadosValidacaoPedido>.Falha("Um ou mais itens indisponíveis");

        var valorTotal = itens.Sum(i => i.PrecoUnitario * i.Quantidade);
        var tempoPreparo = itens.Count * 10; // 10min por item

        return Resultado<DadosValidacaoPedido>.Sucesso(
            new DadosValidacaoPedido(valorTotal, tempoPreparo)
        );
    }
}
```

##### 2. **Serviço de Pagamento**
```csharp
public class ProcessarPagamentoConsumer : IConsumer<ProcessarPagamento>
{
    private readonly IServicoPagamento _servico;

    public async Task Consume(ConsumeContext<ProcessarPagamento> context)
    {
        var resultado = await _servico.ProcessarAsync(
            context.Message.ClienteId,
            context.Message.ValorTotal,
            context.Message.FormaPagamento
        );

        await context.RespondAsync(new PagamentoProcessado(
            context.Message.CorrelacaoId,
            resultado.EhSucesso,
            resultado.EhSucesso ? resultado.Valor.TransacaoId : null,
            resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}

// Implementar também EstornarPagamentoConsumer para compensação
```

##### 3. **Serviço de Entregador**
```csharp
public class AlocarEntregadorConsumer : IConsumer<AlocarEntregador>
{
    private readonly IServicoEntregador _servico;

    public async Task Consume(ConsumeContext<AlocarEntregador> context)
    {
        var resultado = await _servico.AlocarAsync(
            context.Message.RestauranteId,
            context.Message.EnderecoEntrega,
            context.Message.TaxaEntrega
        );

        await context.RespondAsync(new EntregadorAlocado(
            context.Message.CorrelacaoId,
            resultado.EhSucesso,
            resultado.EhSucesso ? resultado.Valor.EntregadorId : null,
            resultado.EhSucesso ? resultado.Valor.TempoEstimadoMinutos : 0,
            resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}

// Implementar também LiberarEntregadorConsumer para compensação
```

##### 4. **Serviço de Notificação**
```csharp
public class NotificarClienteConsumer : IConsumer<NotificarCliente>
{
    private readonly IServicoNotificacao _servico;

    public async Task Consume(ConsumeContext<NotificarCliente> context)
    {
        var resultado = await _servico.EnviarAsync(
            context.Message.ClienteId,
            context.Message.Mensagem,
            context.Message.Tipo
        );

        await context.RespondAsync(new NotificacaoEnviada(
            context.Message.CorrelacaoId,
            resultado.EhSucesso
        ));
    }
}
```

#### 3.4.3 Critérios de Aceitação
- [ ] Todos os consumers implementados
- [ ] Result Pattern usado em toda lógica de negócio
- [ ] Consumers de compensação implementados
- [ ] Logs estruturados em cada operação

---

