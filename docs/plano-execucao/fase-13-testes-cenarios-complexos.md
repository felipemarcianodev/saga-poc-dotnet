# FASE 13: Testes de Cenários Complexos


#### 3.13.1 Objetivos
- Criar testes automatizados para cenários de falha
- Testar compensação em cascata
- Testar resiliência (retry, circuit breaker, timeout)
- Validar idempotência das compensações

#### 3.13.2 Entregas

##### 1. **Testes de Integração com Rebus Test Harness**

```csharp
// Instalar: dotnet add package Rebus.TestFramework

public class PedidoSagaTests
{
    [Fact]
    public async Task DeveCompensarQuandoPagamentoFalhar()
    {
        // Arrange
        await using var provider = new ServiceCollection()
            .AddRebusTestHarness(x =>
            {
                x.AddRebusSaga<PedidoSaga>()
                    .InMemoryRepository();

                x.AddConsumer<ValidarPedidoRestauranteConsumer>();
                x.AddConsumer<ProcessarPagamentoConsumer>();
                x.AddConsumer<CancelarPedidoRestauranteConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Act
        var correlacaoId = NewId.NextGuid();
        await harness.Bus.Publish(new IniciarPedido(
            correlacaoId,
            ClienteId: "CLI_CARTAO_RECUSADO", // Simula falha
            RestauranteId: "REST001",
            Itens: new List<ItemPedido> { new("PROD001", "Pizza", 1, 45.90m) },
            EnderecoEntrega: "Rua Teste, 123",
            FormaPagamento: "CREDITO"
        ));

        // Assert
        var saga = harness.Saga<EstadoPedido, PedidoSaga>();
        Assert.True(await saga.Consumed.Any<IniciarPedido>());

        var instance = saga.Created.ContainsInState(correlacaoId, saga.StateMachine.ExecutandoCompensacao);
        Assert.NotNull(instance);
        Assert.True(instance.EmCompensacao);
        Assert.Contains("RestauranteCancelado", instance.PassosCompensados);
    }

    [Fact]
    public async Task DeveCompensarTudoQuandoEntregadorFalhar()
    {
        // Similar ao teste acima, mas testando falha no entregador
    }

    [Fact]
    public async Task DeveSerIdempotente_QuandoCompensarDuasVezes()
    {
        // Testar que compensação executada 2x não causa duplicidade
    }
}
```

##### 2. **Testes de Carga com NBomber**

```csharp
// Instalar: dotnet add package NBomber

public class TesteCargaSaga
{
    [Fact]
    public void DeveSuportarCargaAlta()
    {
        var scenario = Scenario.Create("saga-load-test", async context =>
        {
            var httpClient = new HttpClient();
            var pedido = new
            {
                clienteId = "CLI001",
                restauranteId = "REST001",
                itens = new[]
                {
                    new { produtoId = "PROD001", nome = "Pizza", quantidade = 1, precoUnitario = 45.90 }
                },
                enderecoEntrega = "Rua Teste, 123",
                formaPagamento = "CREDITO"
            };

            var response = await httpClient.PostAsJsonAsync(
                "http://localhost:5000/api/pedidos",
                pedido
            );

            return response.IsSuccessStatusCode
                ? Response.Ok()
                : Response.Fail();
        })
        .WithLoadSimulations(
            Simulation.Inject(
                rate: 100,  // 100 requisições por segundo
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromMinutes(5)
            )
        );

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        Assert.True(stats.AllRequestCount > 0);
        Assert.True(stats.FailCount < stats.AllRequestCount * 0.01); // Menos de 1% de falha
    }
}
```

##### 3. **Testes de Chaos Engineering**

```csharp
public class TesteChaos
{
    [Fact]
    public async Task DeveContinuarQuandoOrquestradorReiniciar()
    {
        // 1. Iniciar SAGA
        // 2. Parar orquestrador no meio
        // 3. Reiniciar orquestrador
        // 4. Verificar se SAGA recupera estado e continua
    }

    [Fact]
    public async Task DeveUsarCircuitBreakerQuandoServicoFalhar()
    {
        // Simular falha repetida em serviço e verificar se circuit breaker abre
    }
}
```

#### 3.13.3 Critérios de Aceitação
- [ ] Testes de integração cobrem cenários principais
- [ ] Testes de compensação validam ordem reversa
- [ ] Testes de carga validam throughput
- [ ] Testes de caos validam recuperação

---

