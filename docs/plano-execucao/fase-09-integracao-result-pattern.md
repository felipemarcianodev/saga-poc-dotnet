# FASE 9: Integração Completa do Result Pattern no Fluxo de Delivery


#### 3.9.1 Objetivos
- Refatorar todos os serviços para usar Result Pattern de forma consistente
- Criar classes de resultado específicas para cada domínio
- Implementar validações estruturadas com Result Pattern
- Propagar erros de forma elegante através da SAGA

#### 3.9.2 Entregas

##### 1. **Refatoração dos Serviços de Negócio**

**Serviço de Restaurante - Com Result Pattern Completo**
```csharp
// Resultados específicos do domínio
public record DadosValidacaoPedido(
    decimal ValorTotal,
    int TempoPreparoMinutos,
    List<ItemValidado> ItensValidados
);

public record ItemValidado(
    string ProdutoId,
    string Nome,
    int QuantidadeDisponivel,
    decimal PrecoAtual
);

public class ServicoRestaurante : IServicoRestaurante
{
    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar restaurante
        var validacaoRestaurante = await ValidarRestauranteAsync(restauranteId, cancellationToken);
        if (validacaoRestaurante.EhFalha)
            return Resultado<DadosValidacaoPedido>.Falha(validacaoRestaurante.Erro);

        // 2. Validar itens (encadeamento com Bind)
        return await validacaoRestaurante
            .BindAsync(async _ => await ValidarItensAsync(itens, cancellationToken))
            .BindAsync(async itensValidados => await CalcularValorTotalAsync(itensValidados, cancellationToken));
    }

    private async Task<Resultado<bool>> ValidarRestauranteAsync(
        string restauranteId,
        CancellationToken cancellationToken)
    {
        // Simular consulta ao banco
        await Task.Delay(50, cancellationToken);

        return restauranteId switch
        {
            "REST_FECHADO" => Resultado<bool>.Falha(
                Erro.Negocio("Restaurante está fechado no momento", "RESTAURANTE_FECHADO")
            ),
            "REST_INATIVO" => Resultado<bool>.Falha(
                Erro.Negocio("Restaurante temporariamente indisponível", "RESTAURANTE_INATIVO")
            ),
            _ when restauranteId.StartsWith("REST") => Resultado<bool>.Sucesso(true),
            _ => Resultado<bool>.Falha(
                Erro.NaoEncontrado("Restaurante não encontrado", "RESTAURANTE_NAO_ENCONTRADO")
            )
        };
    }

    private async Task<Resultado<List<ItemValidado>>> ValidarItensAsync(
        List<ItemPedido> itens,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);

        var itensValidados = new List<ItemValidado>();
        var erros = new List<Erro>();

        foreach (var item in itens)
        {
            if (item.ProdutoId == "PROD_INDISPONIVEL")
            {
                erros.Add(Erro.Negocio(
                    $"Produto {item.Nome} está indisponível",
                    "PRODUTO_INDISPONIVEL"
                ));
            }
            else if (item.Quantidade > 10)
            {
                erros.Add(Erro.Validacao(
                    $"Quantidade máxima para {item.Nome} é 10 unidades",
                    "QUANTIDADE_EXCEDIDA"
                ));
            }
            else
            {
                itensValidados.Add(new ItemValidado(
                    item.ProdutoId,
                    item.Nome,
                    QuantidadeDisponivel: 50, // Simulado
                    item.PrecoUnitario
                ));
            }
        }

        return erros.Any()
            ? Resultado<List<ItemValidado>>.Falha(erros)
            : Resultado<List<ItemValidado>>.Sucesso(itensValidados);
    }

    private async Task<Resultado<DadosValidacaoPedido>> CalcularValorTotalAsync(
        List<ItemValidado> itensValidados,
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);

        var valorTotal = itensValidados.Sum(i => i.PrecoAtual * i.QuantidadeDisponivel);
        var tempoPreparo = itensValidados.Count * 15; // 15min por item

        return Resultado<DadosValidacaoPedido>.Sucesso(
            new DadosValidacaoPedido(valorTotal, tempoPreparo, itensValidados)
        );
    }

    // Compensação com Result Pattern
    public async Task<Resultado<Unit>> CancelarPedidoAsync(
        string restauranteId,
        Guid pedidoId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        // Simular cancelamento no sistema do restaurante
        Console.WriteLine($"[Restaurante] Cancelando pedido {pedidoId} no restaurante {restauranteId}");

        return Resultado.Sucesso();
    }
}
```

**Serviço de Pagamento - Com Result Pattern Completo**
```csharp
public record DadosPagamento(
    string TransacaoId,
    DateTime DataProcessamento,
    string Autorizacao
);

public class ServicoPagamento : IServicoPagamento
{
    public async Task<Resultado<DadosPagamento>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken = default)
    {
        // 1. Validar valor
        if (valorTotal <= 0)
            return Resultado<DadosPagamento>.Falha(
                Erro.Validacao("Valor do pagamento deve ser maior que zero", "VALOR_INVALIDO")
            );

        if (valorTotal > 1000)
            return Resultado<DadosPagamento>.Falha(
                Erro.Negocio("Valor excede o limite permitido (R$ 1.000)", "VALOR_EXCEDE_LIMITE")
            );

        // 2. Validar forma de pagamento
        var validacaoForma = ValidarFormaPagamento(formaPagamento);
        if (validacaoForma.EhFalha)
            return Resultado<DadosPagamento>.Falha(validacaoForma.Erro);

        // 3. Processar pagamento (simulado)
        await Task.Delay(200, cancellationToken); // Simular latência de gateway

        // Simular diferentes cenários
        return clienteId switch
        {
            "CLI_SEM_SALDO" => Resultado<DadosPagamento>.Falha(
                Erro.Negocio("Saldo insuficiente", "SALDO_INSUFICIENTE")
            ),
            "CLI_CARTAO_RECUSADO" => Resultado<DadosPagamento>.Falha(
                Erro.Negocio("Cartão recusado pela operadora", "CARTAO_RECUSADO")
            ),
            "CLI_TIMEOUT" => Resultado<DadosPagamento>.Falha(
                Erro.Timeout("Timeout ao processar pagamento", "PAGAMENTO_TIMEOUT")
            ),
            _ => Resultado<DadosPagamento>.Sucesso(new DadosPagamento(
                TransacaoId: $"TXN_{Guid.NewGuid():N}",
                DataProcessamento: DateTime.UtcNow,
                Autorizacao: $"AUTH_{Random.Shared.Next(100000, 999999)}"
            ))
        };
    }

    private Resultado<Unit> ValidarFormaPagamento(string formaPagamento)
    {
        var formasValidas = new[] { "CREDITO", "DEBITO", "PIX", "DINHEIRO" };

        return formasValidas.Contains(formaPagamento.ToUpper())
            ? Resultado.Sucesso()
            : Resultado.Falha(
                Erro.Validacao($"Forma de pagamento '{formaPagamento}' não é válida", "FORMA_PAGAMENTO_INVALIDA")
            );
    }

    // Compensação: Estornar pagamento
    public async Task<Resultado<Unit>> EstornarAsync(
        string transacaoId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        if (string.IsNullOrEmpty(transacaoId))
            return Resultado.Falha("TransacaoId não pode ser vazio");

        Console.WriteLine($"[Pagamento] Estornando transação {transacaoId}");

        // Simular estorno
        return Resultado.Sucesso();
    }
}
```

**Serviço de Entregador - Com Result Pattern Completo**
```csharp
public record DadosEntregador(
    string EntregadorId,
    string NomeEntregador,
    int TempoEstimadoMinutos,
    string Veiculo
);

public class ServicoEntregador : IServicoEntregador
{
    public async Task<Resultado<DadosEntregador>> AlocarAsync(
        string restauranteId,
        string enderecoEntrega,
        decimal taxaEntrega,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(150, cancellationToken);

        // Validar taxa
        if (taxaEntrega < 0)
            return Resultado<DadosEntregador>.Falha(
                Erro.Validacao("Taxa de entrega não pode ser negativa", "TAXA_INVALIDA")
            );

        // Simular diferentes cenários
        if (restauranteId == "REST_AREA_REMOTA")
        {
            return Resultado<DadosEntregador>.Falha(
                Erro.Negocio("Nenhum entregador disponível para essa região", "ENTREGADOR_INDISPONIVEL")
            );
        }

        if (enderecoEntrega.Contains("LONGE"))
        {
            return Resultado<DadosEntregador>.Falha(
                Erro.Negocio("Endereço fora da área de cobertura", "AREA_NAO_COBERTA")
            );
        }

        // Alocar entregador
        var entregadorId = $"ENT{Random.Shared.Next(100, 999)}";
        var tempoEstimado = CalcularTempoEstimado(enderecoEntrega);

        return Resultado<DadosEntregador>.Sucesso(new DadosEntregador(
            EntregadorId: entregadorId,
            NomeEntregador: $"Entregador {entregadorId}",
            TempoEstimadoMinutos: tempoEstimado,
            Veiculo: taxaEntrega > 10 ? "Moto" : "Bicicleta"
        ));
    }

    private int CalcularTempoEstimado(string endereco)
    {
        // Simulação simples
        return endereco.Length % 10 + 15; // Entre 15-25 minutos
    }

    // Compensação: Liberar entregador
    public async Task<Resultado<Unit>> LiberarAsync(
        string entregadorId,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);

        Console.WriteLine($"[Entregador] Liberando entregador {entregadorId}");

        return Resultado.Sucesso();
    }
}
```

##### 2. **Atualização dos Consumers para Usar Result Pattern**

```csharp
public class ValidarPedidoRestauranteConsumer : IConsumer<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly ILogger<ValidarPedidoRestauranteConsumer> _logger;

    public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;

        _logger.LogInformation(
            "[Restaurante] Validando pedido {CorrelacaoId} - Restaurante: {RestauranteId}, Itens: {QtdItens}",
            correlacaoId,
            context.Message.RestauranteId,
            context.Message.Itens.Count
        );

        var resultado = await _servico.ValidarPedidoAsync(
            context.Message.RestauranteId,
            context.Message.Itens,
            context.CancellationToken
        );

        // Usar Match para tratar sucesso/falha
        resultado.Match(
            sucesso: dados =>
            {
                _logger.LogInformation(
                    "[Restaurante] Pedido {CorrelacaoId} validado com sucesso - Valor: R$ {Valor:F2}, Tempo: {Tempo}min",
                    correlacaoId,
                    dados.ValorTotal,
                    dados.TempoPreparoMinutos
                );
            },
            falha: erro =>
            {
                _logger.LogWarning(
                    "[Restaurante] Pedido {CorrelacaoId} rejeitado - Motivo: {Motivo} ({Codigo})",
                    correlacaoId,
                    erro.Mensagem,
                    erro.Codigo
                );
            }
        );

        await context.RespondAsync(new PedidoRestauranteValidado(
            correlacaoId,
            Valido: resultado.EhSucesso,
            ValorTotal: resultado.EhSucesso ? resultado.Valor.ValorTotal : 0,
            TempoPreparoMinutos: resultado.EhSucesso ? resultado.Valor.TempoPreparoMinutos : 0,
            MotivoRejeicao: resultado.EhFalha ? resultado.Erro.Mensagem : null
        ));
    }
}
```

##### 3. **Extensão do Result Pattern com Tipos de Erro**

```csharp
// Adicionar em Erro.cs
public enum TipoErro
{
    Validacao,
    Negocio,
    NaoEncontrado,
    Timeout,
    Infraestrutura,
    Externo
}

public partial class Erro
{
    public TipoErro Tipo { get; }
    public string Codigo { get; }
    public Dictionary<string, object>? Metadata { get; }

    // Factory methods específicos
    public static Erro Validacao(string mensagem, string codigo = "VALIDACAO")
        => new(mensagem, TipoErro.Validacao, codigo);

    public static Erro Negocio(string mensagem, string codigo = "NEGOCIO")
        => new(mensagem, TipoErro.Negocio, codigo);

    public static Erro NaoEncontrado(string mensagem, string codigo = "NAO_ENCONTRADO")
        => new(mensagem, TipoErro.NaoEncontrado, codigo);

    public static Erro Timeout(string mensagem, string codigo = "TIMEOUT")
        => new(mensagem, TipoErro.Timeout, codigo);

    public static Erro Infraestrutura(string mensagem, string codigo = "INFRAESTRUTURA")
        => new(mensagem, TipoErro.Infraestrutura, codigo);

    public static Erro Externo(string mensagem, string codigo = "EXTERNO")
        => new(mensagem, TipoErro.Externo, codigo);
}
```

#### 3.9.3 Critérios de Aceitação
- [ ] Todos os serviços usam Result Pattern consistentemente
- [ ] Erros categorizados por tipo (Validação, Negócio, Timeout, etc)
- [ ] Logs estruturados com contexto de Result
- [ ] Consumers tratam Result Pattern adequadamente
- [ ] Compensações retornam Result Pattern

---

