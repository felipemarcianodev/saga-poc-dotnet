using SagaPoc.FluxoCaixa.Domain.Eventos;
using SagaPoc.FluxoCaixa.Domain.ValueObjects;
using SagaPoc.Shared.DDD;
using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Domain.Agregados;

public class Lancamento : AggregateRoot
{
    public Guid Id { get; private set; }
    public EnumTipoLancamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public DateTime DataLancamento { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public string Comerciante { get; private set; } = string.Empty;
    public string? Categoria { get; private set; }
    public EnumStatusLancamento Status { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? AtualizadoEm { get; private set; }

    // Para EF Core
    private Lancamento() { }

    public static Resultado<Lancamento> Criar(
        EnumTipoLancamento tipo,
        decimal valor,
        DateTime dataLancamento,
        string descricao,
        string comerciante,
        string? categoria = null)
    {
        // Validações de domínio
        var validacao = ValidarParametros(tipo, valor, descricao, comerciante);
        if (validacao.EhFalha)
            return Resultado<Lancamento>.Falha(validacao.Erro);

        var lancamento = new Lancamento
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Valor = valor,
            DataLancamento = dataLancamento.Date,
            Descricao = descricao.Trim(),
            Comerciante = comerciante.Trim(),
            Categoria = categoria?.Trim(),
            Status = EnumStatusLancamento.Pendente,
            CriadoEm = DateTime.UtcNow
        };

        // Adicionar evento de domínio
        var evento = tipo == EnumTipoLancamento.Credito
            ? new LancamentoCreditoRegistrado(
                lancamento.Id,
                lancamento.Valor,
                lancamento.DataLancamento,
                lancamento.Descricao,
                lancamento.Comerciante,
                lancamento.Categoria)
            : (object)new LancamentoDebitoRegistrado(
                lancamento.Id,
                lancamento.Valor,
                lancamento.DataLancamento,
                lancamento.Descricao,
                lancamento.Comerciante,
                lancamento.Categoria);

        lancamento.AdicionarEvento(evento);

        return Resultado<Lancamento>.Sucesso(lancamento);
    }

    public Resultado<Unit> Confirmar()
    {
        if (Status == EnumStatusLancamento.Confirmado)
            return Resultado<Unit>.Falha(Erro.Negocio(
                "Lancamento.JaConfirmado",
                "Lançamento já foi confirmado anteriormente"));

        if (Status == EnumStatusLancamento.Cancelado)
            return Resultado<Unit>.Falha(Erro.Negocio(
                "Lancamento.Cancelado",
                "Não é possível confirmar um lançamento cancelado"));

        Status = EnumStatusLancamento.Confirmado;
        AtualizadoEm = DateTime.UtcNow;

        return Resultado.Sucesso();
    }

    public Resultado<Unit> Cancelar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Resultado<Unit>.Falha(Erro.Validacao(
                "Lancamento.MotivoObrigatorio",
                "O motivo do cancelamento é obrigatório"));

        if (Status == EnumStatusLancamento.Cancelado)
            return Resultado<Unit>.Falha(Erro.Negocio(
                "Lancamento.JaCancelado",
                "Lançamento já foi cancelado anteriormente"));

        Status = EnumStatusLancamento.Cancelado;
        AtualizadoEm = DateTime.UtcNow;

        AdicionarEvento(new LancamentoCancelado(Id, motivo, DateTime.UtcNow));

        return Resultado.Sucesso();
    }

    private static Resultado<Unit> ValidarParametros(
        EnumTipoLancamento tipo,
        decimal valor,
        string descricao,
        string comerciante)
    {
        if (valor <= 0)
            return Resultado<Unit>.Falha(Erro.Validacao(
                "Lancamento.ValorInvalido",
                "O valor do lançamento deve ser maior que zero"));

        if (valor > 1_000_000_000) // 1 bilhão
            return Resultado<Unit>.Falha(Erro.Validacao(
                "Lancamento.ValorExcessivo",
                "O valor do lançamento excede o limite permitido"));

        if (string.IsNullOrWhiteSpace(descricao))
            return Resultado<Unit>.Falha(Erro.Validacao(
                "Lancamento.DescricaoObrigatoria",
                "A descrição do lançamento é obrigatória"));

        if (descricao.Length > 500)
            return Resultado<Unit>.Falha(Erro.Validacao(
                "Lancamento.DescricaoMuitoLonga",
                "A descrição não pode exceder 500 caracteres"));

        if (string.IsNullOrWhiteSpace(comerciante))
            return Resultado<Unit>.Falha(Erro.Validacao(
                "Lancamento.ComercianteObrigatorio",
                "O identificador do comerciante é obrigatório"));

        return Resultado.Sucesso();
    }
}
