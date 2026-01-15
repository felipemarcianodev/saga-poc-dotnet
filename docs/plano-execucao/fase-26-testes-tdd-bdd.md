# FASE 26: Testes TDD/BDD e Validação de NFRs

## Objetivos

- Implementar testes unitários com cobertura > 80%
- Criar testes de integração para fluxos completos
- Aplicar TDD (Test-Driven Development) nas regras de negócio
- Implementar testes BDD (Behavior-Driven Development) com SpecFlow
- Validar NFRs (50 req/s, disponibilidade, resiliência)
- Testes de carga com NBomber
- Garantir qualidade e confiabilidade do código

## Entregas

### 1. **Estrutura de Testes**

```
tests/
├── SagaPoc.FluxoCaixa.Domain.Tests/           # Testes Unitários
│   ├── Agregados/
│   │   ├── LancamentoTests.cs
│   │   └── ConsolidadoDiarioTests.cs
│   └── ValueObjects/
│       └── TipoLancamentoTests.cs
│
├── SagaPoc.FluxoCaixa.Application.Tests/      # Testes de Serviços
│   ├── Handlers/
│   │   ├── RegistrarLancamentoHandlerTests.cs
│   │   ├── LancamentoCreditoHandlerTests.cs
│   │   └── LancamentoDebitoHandlerTests.cs
│   └── Servicos/
│       └── ConsolidacaoServicoTests.cs
│
├── SagaPoc.FluxoCaixa.Integration.Tests/      # Testes de Integração
│   ├── Api/
│   │   ├── LancamentosControllerTests.cs
│   │   └── ConsolidadoControllerTests.cs
│   ├── Persistencia/
│   │   ├── LancamentoRepositoryTests.cs
│   │   └── ConsolidadoDiarioRepositoryTests.cs
│   └── Mensageria/
│       └── EventosDominioTests.cs
│
├── SagaPoc.FluxoCaixa.BDD.Tests/              # Testes BDD (SpecFlow)
│   ├── Features/
│   │   ├── RegistrarLancamento.feature
│   │   ├── ConsultarConsolidado.feature
│   │   └── Resiliencia.feature
│   └── StepDefinitions/
│       ├── LancamentoSteps.cs
│       └── ConsolidadoSteps.cs
│
└── SagaPoc.FluxoCaixa.LoadTests/              # Testes de Carga (NBomber)
    ├── LancamentosLoadTests.cs
    └── ConsolidadoLoadTests.cs
```

### 2. **Testes Unitários - Domínio**

#### Testes do Agregado Lancamento

```csharp
// tests/SagaPoc.FluxoCaixa.Domain.Tests/Agregados/LancamentoTests.cs

using FluentAssertions;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.ValueObjects;
using Xunit;

namespace SagaPoc.FluxoCaixa.Domain.Tests.Agregados;

public class LancamentoTests
{
    [Fact]
    public void Criar_ComParametrosValidos_DeveRetornarSucesso()
    {
        // Arrange
        var tipo = EnumTipoLancamento.Credito;
        var valor = 150.75m;
        var data = new DateTime(2026, 1, 15);
        var descricao = "Venda à vista";
        var comerciante = "COM001";

        // Act
        var resultado = Lancamento.Criar(tipo, valor, data, descricao, comerciante);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().NotBeNull();
        resultado.Value.Tipo.Should().Be(tipo);
        resultado.Value.Valor.Should().Be(valor);
        resultado.Value.DataLancamento.Should().Be(data.Date);
        resultado.Value.Descricao.Should().Be(descricao);
        resultado.Value.Comerciante.Should().Be(comerciante);
        resultado.Value.Status.Should().Be(EnumStatusLancamento.Pendente);
        resultado.Value.EventosDominio.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0, "Lancamento.ValorInvalido")]
    [InlineData(-10.50, "Lancamento.ValorInvalido")]
    [InlineData(-0.01, "Lancamento.ValorInvalido")]
    public void Criar_ComValorInvalidoOuZero_DeveRetornarFalha(decimal valor, string codigoErroEsperado)
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Debito,
            valor,
            DateTime.Today,
            "Descrição válida",
            "COM001");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be(codigoErroEsperado);
        resultado.Error.Mensagem.Should().Contain("maior que zero");
    }

    [Fact]
    public void Criar_ComValorExcessivo_DeveRetornarFalha()
    {
        // Arrange
        var valorExcessivo = 1_500_000_000m; // Acima do limite

        // Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            valorExcessivo,
            DateTime.Today,
            "Descrição",
            "COM001");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.ValorExcessivo");
    }

    [Theory]
    [InlineData(null, "Lancamento.DescricaoObrigatoria")]
    [InlineData("", "Lancamento.DescricaoObrigatoria")]
    [InlineData("   ", "Lancamento.DescricaoObrigatoria")]
    public void Criar_ComDescricaoInvalida_DeveRetornarFalha(string descricao, string codigoErro)
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            descricao,
            "COM001");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be(codigoErro);
    }

    [Fact]
    public void Criar_ComDescricaoMuitoLonga_DeveRetornarFalha()
    {
        // Arrange
        var descricaoLonga = new string('A', 501);

        // Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            descricaoLonga,
            "COM001");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.DescricaoMuitoLonga");
    }

    [Fact]
    public void Confirmar_LancamentoPendente_DeveAlterarStatusParaConfirmado()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Value;

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        lancamento.Status.Should().Be(EnumStatusLancamento.Confirmado);
        lancamento.AtualizadoEm.Should().NotBeNull();
    }

    [Fact]
    public void Confirmar_LancamentoJaConfirmado_DeveRetornarFalha()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Value;

        lancamento.Confirmar();

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.JaConfirmado");
    }

    [Fact]
    public void Cancelar_LancamentoPendente_DeveAlterarStatusParaCancelado()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Value;

        var motivo = "Cliente solicitou cancelamento";

        // Act
        var resultado = lancamento.Cancelar(motivo);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        lancamento.Status.Should().Be(EnumStatusLancamento.Cancelado);
        lancamento.EventosDominio.Should().HaveCount(2); // Criação + Cancelamento
    }

    [Fact]
    public void Cancelar_SemMotivo_DeveRetornarFalha()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Value;

        // Act
        var resultado = lancamento.Cancelar("");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.MotivoObrigatorio");
    }
}
```

#### Testes do Agregado ConsolidadoDiario

```csharp
// tests/SagaPoc.FluxoCaixa.Domain.Tests/Agregados/ConsolidadoDiarioTests.cs

using FluentAssertions;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using Xunit;

namespace SagaPoc.FluxoCaixa.Domain.Tests.Agregados;

public class ConsolidadoDiarioTests
{
    [Fact]
    public void Criar_ComParametrosValidos_DeveInicializarComZeros()
    {
        // Arrange
        var data = new DateTime(2026, 1, 15);
        var comerciante = "COM001";

        // Act
        var consolidado = ConsolidadoDiario.Criar(data, comerciante);

        // Assert
        consolidado.Data.Should().Be(data.Date);
        consolidado.Comerciante.Should().Be(comerciante);
        consolidado.TotalCreditos.Should().Be(0);
        consolidado.TotalDebitos.Should().Be(0);
        consolidado.SaldoDiario.Should().Be(0);
        consolidado.QuantidadeCreditos.Should().Be(0);
        consolidado.QuantidadeDebitos.Should().Be(0);
    }

    [Fact]
    public void AplicarCredito_ComValorValido_DeveIncrementarTotais()
    {
        // Arrange
        var consolidado = ConsolidadoDiario.Criar(DateTime.Today, "COM001");

        // Act
        consolidado.AplicarCredito(100m);
        consolidado.AplicarCredito(50m);

        // Assert
        consolidado.TotalCreditos.Should().Be(150m);
        consolidado.QuantidadeCreditos.Should().Be(2);
        consolidado.SaldoDiario.Should().Be(150m);
    }

    [Fact]
    public void AplicarDebito_ComValorValido_DeveIncrementarTotais()
    {
        // Arrange
        var consolidado = ConsolidadoDiario.Criar(DateTime.Today, "COM001");

        // Act
        consolidado.AplicarDebito(75m);
        consolidado.AplicarDebito(25m);

        // Assert
        consolidado.TotalDebitos.Should().Be(100m);
        consolidado.QuantidadeDebitos.Should().Be(2);
        consolidado.SaldoDiario.Should().Be(-100m);
    }

    [Fact]
    public void SaldoDiario_ComCreditosEDebitos_DeveCalcularCorretamente()
    {
        // Arrange
        var consolidado = ConsolidadoDiario.Criar(DateTime.Today, "COM001");

        // Act
        consolidado.AplicarCredito(500m);
        consolidado.AplicarCredito(300m);
        consolidado.AplicarDebito(200m);
        consolidado.AplicarDebito(100m);

        // Assert
        consolidado.TotalCreditos.Should().Be(800m);
        consolidado.TotalDebitos.Should().Be(300m);
        consolidado.SaldoDiario.Should().Be(500m);
        consolidado.QuantidadeTotalLancamentos.Should().Be(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void AplicarCredito_ComValorInvalido_DeveLancarExcecao(decimal valor)
    {
        // Arrange
        var consolidado = ConsolidadoDiario.Criar(DateTime.Today, "COM001");

        // Act
        Action act = () => consolidado.AplicarCredito(valor);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*positivo*");
    }
}
```

### 3. **Testes de Integração**

```csharp
// tests/SagaPoc.FluxoCaixa.Integration.Tests/Api/LancamentosControllerTests.cs

using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace SagaPoc.FluxoCaixa.Integration.Tests.Api;

public class LancamentosControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public LancamentosControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Registrar_ComDadosValidos_DeveRetornar202Accepted()
    {
        // Arrange
        var request = new
        {
            tipo = "Credito",
            valor = 100.50,
            dataLancamento = DateTime.Today,
            descricao = "Venda teste",
            comerciante = "COM001"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/lancamentos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        content.Should().NotBeNull();
        ((object)content.correlationId).Should().NotBeNull();
    }

    [Fact]
    public async Task Registrar_ComValorInvalido_DeveRetornar400BadRequest()
    {
        // Arrange
        var request = new
        {
            tipo = "Credito",
            valor = -50,
            dataLancamento = DateTime.Today,
            descricao = "Teste",
            comerciante = "COM001"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/lancamentos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ObterPorId_LancamentoExistente_DeveRetornar200Ok()
    {
        // Arrange
        var lancamentoId = await CriarLancamentoTesteAsync();

        // Act
        var response = await _client.GetAsync($"/api/lancamentos/{lancamentoId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lancamento = await response.Content.ReadFromJsonAsync<LancamentoResponse>();
        lancamento.Should().NotBeNull();
        lancamento.Id.Should().Be(lancamentoId);
    }

    [Fact]
    public async Task ObterPorId_LancamentoInexistente_DeveRetornar404NotFound()
    {
        // Arrange
        var idInexistente = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/lancamentos/{idInexistente}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CriarLancamentoTesteAsync()
    {
        var request = new
        {
            tipo = "Credito",
            valor = 100,
            dataLancamento = DateTime.Today,
            descricao = "Teste",
            comerciante = "COM001"
        };

        var response = await _client.PostAsJsonAsync("/api/lancamentos", request);
        var content = await response.Content.ReadFromJsonAsync<dynamic>();

        return Guid.Parse(content.correlationId.ToString());
    }
}
```

### 4. **Testes BDD com SpecFlow**

```gherkin
# tests/SagaPoc.FluxoCaixa.BDD.Tests/Features/RegistrarLancamento.feature

Feature: Registrar Lançamento
  Como um comerciante
  Eu quero registrar lançamentos de débito e crédito
  Para controlar meu fluxo de caixa diário

  Scenario: Registrar crédito com sucesso
    Given que sou o comerciante "COM001"
    When eu registro um crédito de R$ 150.00 com a descrição "Venda à vista"
    Then o lançamento deve ser aceito
    And o status inicial deve ser "Pendente"
    And um evento de crédito deve ser publicado

  Scenario: Registrar débito com sucesso
    Given que sou o comerciante "COM001"
    When eu registro um débito de R$ 75.50 com a descrição "Compra de insumos"
    Then o lançamento deve ser aceito
    And um evento de débito deve ser publicado

  Scenario: Tentar registrar lançamento com valor negativo
    Given que sou o comerciante "COM001"
    When eu tento registrar um crédito de R$ -50.00
    Then o lançamento deve ser rejeitado
    And a mensagem de erro deve conter "valor deve ser maior que zero"

  Scenario: Tentar registrar lançamento sem descrição
    Given que sou o comerciante "COM001"
    When eu tento registrar um crédito de R$ 100.00 sem descrição
    Then o lançamento deve ser rejeitado
    And a mensagem de erro deve conter "descrição é obrigatória"
```

```gherkin
# tests/SagaPoc.FluxoCaixa.BDD.Tests/Features/ConsultarConsolidado.feature

Feature: Consultar Consolidado Diário
  Como um comerciante
  Eu quero consultar o saldo consolidado do dia
  Para acompanhar meu fluxo de caixa

  Background:
    Given que sou o comerciante "COM001"
    And a data é "2026-01-15"

  Scenario: Consultar consolidado com lançamentos
    Given que existem os seguintes lançamentos:
      | Tipo    | Valor  | Descrição         |
      | Credito | 500.00 | Venda 1          |
      | Credito | 300.00 | Venda 2          |
      | Debito  | 150.00 | Compra insumos   |
      | Debito  | 100.00 | Pagamento fornec |
    When eu consulto o consolidado do dia
    Then o total de créditos deve ser R$ 800.00
    And o total de débitos deve ser R$ 250.00
    And o saldo diário deve ser R$ 550.00
    And a quantidade de lançamentos deve ser 4

  Scenario: Consultar consolidado sem lançamentos
    Given que não existem lançamentos para o dia
    When eu consulto o consolidado do dia
    Then deve retornar erro de consolidado não encontrado
```

```csharp
// tests/SagaPoc.FluxoCaixa.BDD.Tests/StepDefinitions/LancamentoSteps.cs

using TechTalk.SpecFlow;
using FluentAssertions;

namespace SagaPoc.FluxoCaixa.BDD.Tests.StepDefinitions;

[Binding]
public class LancamentoSteps
{
    private readonly ScenarioContext _context;
    private string _comerciante;
    private Result<Lancamento> _resultado;

    public LancamentoSteps(ScenarioContext context)
    {
        _context = context;
    }

    [Given(@"que sou o comerciante ""(.*)""")]
    public void GivenQueSouOComerciante(string comerciante)
    {
        _comerciante = comerciante;
    }

    [When(@"eu registro um crédito de R\$ (.*) com a descrição ""(.*)""")]
    public void WhenEuRegistroUmCredito(decimal valor, string descricao)
    {
        _resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            valor,
            DateTime.Today,
            descricao,
            _comerciante);
    }

    [Then(@"o lançamento deve ser aceito")]
    public void ThenOLancamentoDeveSerAceito()
    {
        _resultado.IsSuccess.Should().BeTrue();
    }

    [Then(@"o status inicial deve ser ""(.*)""")]
    public void ThenOStatusInicialDeveSer(string statusEsperado)
    {
        _resultado.Value.Status.ToString().Should().Be(statusEsperado);
    }

    [Then(@"um evento de crédito deve ser publicado")]
    public void ThenUmEventoDeCreditoDeveSerPublicado()
    {
        _resultado.Value.EventosDominio
            .Should().ContainSingle()
            .Which.Should().BeOfType<LancamentoCreditoRegistrado>();
    }
}
```

### 5. **Testes de Carga com NBomber**

```csharp
// tests/SagaPoc.FluxoCaixa.LoadTests/ConsolidadoLoadTests.cs

using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace SagaPoc.FluxoCaixa.LoadTests;

public class ConsolidadoLoadTests
{
    [Fact]
    public void ConsolidadoDiario_DeveSuportar50RequisicoesPorSegundo()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

        var scenario = Scenario.Create("consultar_consolidado", async context =>
        {
            var request = Http.CreateRequest("GET", "/api/consolidado/COM001/2026-01-15")
                .WithHeader("Accept", "application/json");

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithLoadSimulations(
            Simulation.Inject(
                rate: 50,                    // 50 requisições por segundo
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromMinutes(1)  // Durante 1 minuto
            )
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert
        var scnStats = stats.ScenarioStats[0];

        // NFR: Máximo 5% de perda
        var sucessRate = (double)scnStats.OkCount / scnStats.AllRequestCount * 100;
        sucessRate.Should().BeGreaterThanOrEqualTo(95.0);

        // Latência P95 < 100ms (com cache)
        scnStats.LatencyCount.Percent95.Should().BeLessThan(100);

        // Todas as requisições devem retornar 200
        scnStats.Fail.StatusCodes.Should().BeEmpty();
    }

    [Fact]
    public void RegistrarLancamento_DeveProcessarCargaSemPerda()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

        var scenario = Scenario.Create("registrar_lancamento", async context =>
        {
            var payload = new
            {
                tipo = "Credito",
                valor = 100.00,
                dataLancamento = DateTime.Today,
                descricao = $"Load test {context.InvocationNumber}",
                comerciante = "COM001"
            };

            var request = Http.CreateRequest("POST", "/api/lancamentos")
                .WithJsonBody(payload);

            var response = await Http.Send(httpClient, request);

            return response;
        })
        .WithLoadSimulations(
            Simulation.Inject(
                rate: 100,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromMinutes(2)
            )
        );

        // Act
        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .Run();

        // Assert
        var scnStats = stats.ScenarioStats[0];

        // Taxa de sucesso > 99%
        var sucessRate = (double)scnStats.OkCount / scnStats.AllRequestCount * 100;
        sucessRate.Should().BeGreaterThanOrEqualTo(99.0);
    }
}
```

### 6. **Testes de Resiliência**

```csharp
// tests/SagaPoc.FluxoCaixa.Integration.Tests/Resiliencia/DisponibilidadeTests.cs

using FluentAssertions;
using Xunit;

namespace SagaPoc.FluxoCaixa.Integration.Tests.Resiliencia;

public class DisponibilidadeTests
{
    [Fact]
    public async Task Lancamentos_DevePermanecer Disponivel_QuandoConsolidadoCair()
    {
        // Arrange
        await PararServicoConsolidadoAsync();

        var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

        var request = new
        {
            tipo = "Credito",
            valor = 100,
            dataLancamento = DateTime.Today,
            descricao = "Teste resiliência",
            comerciante = "COM001"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/lancamentos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Cleanup
        await IniciarServicoConsolidadoAsync();
    }

    [Fact]
    public async Task Consolidado_DeveProcessarEventos_AposRecuperacao()
    {
        // Arrange
        var lancamentosIds = await CriarLancamentosDuranteIndisponibilidadeAsync();

        await IniciarServicoConsolidadoAsync();

        // Act - Aguardar processamento assíncrono
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Assert - Verificar se todos os lançamentos foram consolidados
        var consolidado = await ObterConsolidadoAsync(DateTime.Today, "COM001");

        consolidado.QuantidadeTotalLancamentos.Should().Be(lancamentosIds.Count);
    }
}
```

## Cobertura de Código

### Meta de Cobertura

- **Domínio**: > 95%
- **Application (Handlers)**: > 85%
- **Infrastructure**: > 70%
- **API**: > 80%
- **Geral**: > 80%

### Ferramentas

```bash
# Instalar ferramenta de cobertura
dotnet tool install --global dotnet-coverage

# Executar testes com cobertura
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Gerar relatório HTML
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:./coverage-report
```

## Critérios de Aceitação

- [ ] Testes unitários implementados para todos os agregados
- [ ] Cobertura de código > 80%
- [ ] Testes de integração para API implementados
- [ ] Testes BDD com SpecFlow implementados
- [ ] Testes de carga validando NFR de 50 req/s
- [ ] Taxa de sucesso > 95% nos testes de carga
- [ ] Testes de resiliência validando disponibilidade independente
- [ ] Latência P95 < 100ms no consolidado (com cache)
- [ ] Todos os testes passando no pipeline CI/CD

## Trade-offs

**Benefícios:**
- Alta confiança na qualidade do código
- Documentação viva (BDD)
- Validação de NFRs
- Detecção precoce de regressões

**Considerações:**
- Tempo inicial de desenvolvimento maior
- Manutenção de testes
- Curva de aprendizado (SpecFlow, NBomber)

## Estimativa

**Tempo Total**: 12-16 horas

- Testes unitários (domínio): 3-4 horas
- Testes de integração: 3-4 horas
- Testes BDD (SpecFlow): 3-4 horas
- Testes de carga (NBomber): 2-3 horas
- Configuração e documentação: 1-2 horas

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
