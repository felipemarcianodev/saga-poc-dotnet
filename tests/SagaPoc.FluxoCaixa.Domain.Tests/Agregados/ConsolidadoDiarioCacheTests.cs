using System.Text.Json;
using FluentAssertions;
using SagaPoc.FluxoCaixa.Application.Services;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using Xunit;

namespace SagaPoc.FluxoCaixa.Domain.Tests.Agregados;

public class ConsolidadoDiarioCacheTests
{
    private static ConsolidadoDiario CriarConsolidado()
    {
        var consolidado = ConsolidadoDiario.Criar(new DateTime(2026, 10, 20), "COM001");
        consolidado.AplicarCredito(150.50m);
        consolidado.AplicarCredito(49.50m);
        consolidado.AplicarDebito(40.25m);
        return consolidado;
    }

    [Fact]
    public void Reconstituir_DeveManterTodosOsValores()
    {
        // Arrange
        var id = Guid.NewGuid();
        var atualizacao = new DateTime(2026, 10, 20, 18, 30, 0, DateTimeKind.Utc);

        // Act
        var consolidado = ConsolidadoDiario.Reconstituir(
            id, new DateTime(2026, 10, 20, 15, 0, 0), "COM001", 200m, 40.25m, 2, 1, atualizacao);

        // Assert
        consolidado.Id.Should().Be(id);
        consolidado.Data.Should().Be(new DateTime(2026, 10, 20));
        consolidado.Comerciante.Should().Be("COM001");
        consolidado.TotalCreditos.Should().Be(200m);
        consolidado.TotalDebitos.Should().Be(40.25m);
        consolidado.SaldoDiario.Should().Be(159.75m);
        consolidado.QuantidadeTotalLancamentos.Should().Be(3);
        consolidado.UltimaAtualizacao.Should().Be(atualizacao);
    }

    [Fact]
    public void Cache_AposIdaEVoltaPeloJson_DeveDevolverOMesmoConsolidado()
    {
        // Arrange
        var original = CriarConsolidado();
        var json = JsonSerializer.Serialize(ConsolidadoDiarioCache.De(original));

        // Act
        var lido = JsonSerializer.Deserialize<ConsolidadoDiarioCache>(json)!.ParaAgregado();

        // Assert
        lido.Should().BeEquivalentTo(original, o => o.Excluding(c => c.EventosDominio));
    }

    [Fact]
    public void Agregado_NaoEhDesserializavel_PorIssoOCacheUsaOSnapshot()
    {
        // O defeito original: o Redis recebia o agregado e nunca conseguia lê-lo de volta.
        var json = JsonSerializer.Serialize(CriarConsolidado());

        var desserializar = () => JsonSerializer.Deserialize<ConsolidadoDiario>(json);

        desserializar.Should().Throw<NotSupportedException>();
    }
}
