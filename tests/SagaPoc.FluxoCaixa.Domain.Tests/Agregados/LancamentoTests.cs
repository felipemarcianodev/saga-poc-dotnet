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
        var valor = 100.50m;
        var data = DateTime.Today;
        var descricao = "Venda de produto X";
        var comerciante = "COM001";

        // Act
        var resultado = Lancamento.Criar(tipo, valor, data, descricao, comerciante);

        // Assert
        resultado.EhSucesso.Should().BeTrue();
        resultado.Valor.Tipo.Should().Be(tipo);
        resultado.Valor.Valor.Should().Be(valor);
        resultado.Valor.Descricao.Should().Be(descricao);
        resultado.Valor.Status.Should().Be(EnumStatusLancamento.Pendente);
        resultado.Valor.EventosDominio.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_ComValorInvalido_DeveRetornarFalha(decimal valorInvalido)
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Debito,
            valorInvalido,
            DateTime.Today,
            "Descrição",
            "COM001");

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.ValorInvalido");
    }

    [Fact]
    public void Criar_ComDescricaoVazia_DeveRetornarFalha()
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "",
            "COM001");

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.DescricaoObrigatoria");
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
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.DescricaoMuitoLonga");
    }

    [Fact]
    public void Criar_ComComercianteVazio_DeveRetornarFalha()
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Descrição",
            "");

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.ComercianteObrigatorio");
    }

    [Fact]
    public void Criar_ComValorExcessivo_DeveRetornarFalha()
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            1_000_000_001m,
            DateTime.Today,
            "Descrição",
            "COM001");

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.ValorExcessivo");
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
            "COM001").Valor;

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.EhSucesso.Should().BeTrue();
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
            "COM001").Valor;

        lancamento.Confirmar();

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.JaConfirmado");
    }

    [Fact]
    public void Confirmar_LancamentoCancelado_DeveRetornarFalha()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Valor;

        lancamento.Cancelar("Motivo do cancelamento");

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.Cancelado");
    }

    [Fact]
    public void Cancelar_ComMotivoValido_DeveAlterarStatusParaCancelado()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Valor;

        // Act
        var resultado = lancamento.Cancelar("Motivo do cancelamento");

        // Assert
        resultado.EhSucesso.Should().BeTrue();
        lancamento.Status.Should().Be(EnumStatusLancamento.Cancelado);
        lancamento.AtualizadoEm.Should().NotBeNull();
        lancamento.EventosDominio.Should().HaveCount(2); // 1 de criação + 1 de cancelamento
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
            "COM001").Valor;

        // Act
        var resultado = lancamento.Cancelar("");

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.MotivoObrigatorio");
    }

    [Fact]
    public void Cancelar_LancamentoJaCancelado_DeveRetornarFalha()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Valor;

        lancamento.Cancelar("Primeiro cancelamento");

        // Act
        var resultado = lancamento.Cancelar("Segundo cancelamento");

        // Assert
        resultado.EhFalha.Should().BeTrue();
        resultado.Erro.Codigo.Should().Be("Lancamento.JaCancelado");
    }

    [Fact]
    public void Criar_LancamentoCredito_DeveGerarEventoCreditoRegistrado()
    {
        // Arrange & Act
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Valor;

        // Assert
        lancamento.EventosDominio.Should().HaveCount(1);
        lancamento.EventosDominio.First().Should().BeOfType<SagaPoc.FluxoCaixa.Domain.Eventos.LancamentoCreditoRegistrado>();
    }

    [Fact]
    public void Criar_LancamentoDebito_DeveGerarEventoDebitoRegistrado()
    {
        // Arrange & Act
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Debito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Valor;

        // Assert
        lancamento.EventosDominio.Should().HaveCount(1);
        lancamento.EventosDominio.First().Should().BeOfType<SagaPoc.FluxoCaixa.Domain.Eventos.LancamentoDebitoRegistrado>();
    }
}
