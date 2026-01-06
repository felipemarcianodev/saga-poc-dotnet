using System.Diagnostics.CodeAnalysis;

namespace SagaPoc.Shared.ResultPattern;

/// <summary>
/// Representa o resultado de uma operação que pode ter sucesso ou falha.
/// Implementa o padrão Result (Railway-Oriented Programming).
/// </summary>
/// <typeparam name="T">Tipo do valor retornado em caso de sucesso.</typeparam>
public sealed class Resultado<T>
{
    private readonly T? _valor;
    private readonly Erro? _erro;
    private readonly List<Erro>? _erros;

    /// <summary>
    /// Indica se a operação foi bem-sucedida.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_valor))]
    [MemberNotNullWhen(true, nameof(Valor))]
    [MemberNotNullWhen(false, nameof(_erro))]
    [MemberNotNullWhen(false, nameof(Erro))]
    public bool EhSucesso { get; }

    /// <summary>
    /// Indica se a operação falhou.
    /// </summary>
    [MemberNotNullWhen(false, nameof(_valor))]
    [MemberNotNullWhen(false, nameof(Valor))]
    [MemberNotNullWhen(true, nameof(_erro))]
    [MemberNotNullWhen(true, nameof(Erro))]
    public bool EhFalha => !EhSucesso;

    /// <summary>
    /// Obtém o valor retornado pela operação (disponível apenas em caso de sucesso).
    /// </summary>
    /// <exception cref="InvalidOperationException">Lançada quando acessada em caso de falha.</exception>
    public T Valor
    {
        get
        {
            if (EhFalha)
                throw new InvalidOperationException("Não é possível acessar o valor de um resultado com falha.");

            return _valor;
        }
    }

    /// <summary>
    /// Obtém o erro principal (disponível apenas em caso de falha).
    /// </summary>
    /// <exception cref="InvalidOperationException">Lançada quando acessada em caso de sucesso.</exception>
    public Erro Erro
    {
        get
        {
            if (EhSucesso)
                throw new InvalidOperationException("Não é possível acessar o erro de um resultado bem-sucedido.");

            return _erro;
        }
    }

    /// <summary>
    /// Obtém todos os erros (disponível apenas em caso de falha com múltiplos erros).
    /// </summary>
    public IReadOnlyList<Erro> Erros => _erros?.AsReadOnly() ?? new List<Erro> { Erro }.AsReadOnly();

    private Resultado(T valor)
    {
        EhSucesso = true;
        _valor = valor;
        _erro = default;
        _erros = null;
    }

    private Resultado(Erro erro)
    {
        EhSucesso = false;
        _valor = default;
        _erro = erro;
        _erros = null;
    }

    private Resultado(List<Erro> erros)
    {
        if (erros == null || erros.Count == 0)
            throw new ArgumentException("A lista de erros não pode ser nula ou vazia.", nameof(erros));

        EhSucesso = false;
        _valor = default;
        _erro = erros[0]; // Primeiro erro como principal
        _erros = erros;
    }

    /// <summary>
    /// Cria um resultado de sucesso.
    /// </summary>
    /// <param name="valor">Valor retornado pela operação.</param>
    /// <returns>Instância de <see cref="Resultado{T}"/> representando sucesso.</returns>
    public static Resultado<T> Sucesso(T valor) => new(valor);

    /// <summary>
    /// Cria um resultado de falha com um único erro.
    /// </summary>
    /// <param name="erro">Erro que causou a falha.</param>
    /// <returns>Instância de <see cref="Resultado{T}"/> representando falha.</returns>
    public static Resultado<T> Falha(Erro erro) => new(erro);

    /// <summary>
    /// Cria um resultado de falha com um único erro (mensagem simples).
    /// </summary>
    /// <param name="mensagem">Mensagem de erro.</param>
    /// <returns>Instância de <see cref="Resultado{T}"/> representando falha.</returns>
    public static Resultado<T> Falha(string mensagem) => new(Erro.Negocio(mensagem));

    /// <summary>
    /// Cria um resultado de falha com múltiplos erros.
    /// </summary>
    /// <param name="erros">Lista de erros.</param>
    /// <returns>Instância de <see cref="Resultado{T}"/> representando falha.</returns>
    public static Resultado<T> Falha(List<Erro> erros) => new(erros);

    /// <summary>
    /// Transforma o valor de sucesso usando uma função de mapeamento.
    /// </summary>
    /// <typeparam name="TOut">Tipo do valor de saída.</typeparam>
    /// <param name="funcaoMapeamento">Função para transformar o valor.</param>
    /// <returns>Novo resultado com o valor transformado.</returns>
    public Resultado<TOut> Map<TOut>(Func<T, TOut> funcaoMapeamento)
    {
        return EhSucesso
            ? Resultado<TOut>.Sucesso(funcaoMapeamento(Valor))
            : Resultado<TOut>.Falha(Erro);
    }

    /// <summary>
    /// Encadeia uma operação que retorna outro Resultado (flatMap/bind).
    /// </summary>
    /// <typeparam name="TOut">Tipo do valor de saída.</typeparam>
    /// <param name="funcaoProxima">Função que retorna o próximo resultado.</param>
    /// <returns>Resultado da operação encadeada.</returns>
    public Resultado<TOut> Bind<TOut>(Func<T, Resultado<TOut>> funcaoProxima)
    {
        return EhSucesso
            ? funcaoProxima(Valor)
            : Resultado<TOut>.Falha(Erro);
    }

    /// <summary>
    /// Encadeia uma operação assíncrona que retorna outro Resultado.
    /// </summary>
    /// <typeparam name="TOut">Tipo do valor de saída.</typeparam>
    /// <param name="funcaoProxima">Função assíncrona que retorna o próximo resultado.</param>
    /// <returns>Task contendo o resultado da operação encadeada.</returns>
    public async Task<Resultado<TOut>> BindAsync<TOut>(Func<T, Task<Resultado<TOut>>> funcaoProxima)
    {
        return EhSucesso
            ? await funcaoProxima(Valor)
            : Resultado<TOut>.Falha(Erro);
    }

    /// <summary>
    /// Executa uma ação baseada no resultado (pattern matching).
    /// </summary>
    /// <typeparam name="TOut">Tipo do valor de retorno.</typeparam>
    /// <param name="sucesso">Função executada em caso de sucesso.</param>
    /// <param name="falha">Função executada em caso de falha.</param>
    /// <returns>Valor retornado pela função correspondente.</returns>
    public TOut Match<TOut>(Func<T, TOut> sucesso, Func<Erro, TOut> falha)
    {
        return EhSucesso ? sucesso(Valor) : falha(Erro);
    }

    /// <summary>
    /// Executa uma ação sem retorno baseada no resultado.
    /// </summary>
    /// <param name="sucesso">Ação executada em caso de sucesso.</param>
    /// <param name="falha">Ação executada em caso de falha.</param>
    public void Match(Action<T> sucesso, Action<Erro> falha)
    {
        if (EhSucesso)
            sucesso(Valor);
        else
            falha(Erro);
    }

    /// <summary>
    /// Executa uma ação apenas em caso de sucesso.
    /// </summary>
    /// <param name="acao">Ação a ser executada.</param>
    /// <returns>O próprio resultado (para encadeamento).</returns>
    public Resultado<T> EmSucesso(Action<T> acao)
    {
        if (EhSucesso)
            acao(Valor);

        return this;
    }

    /// <summary>
    /// Executa uma ação apenas em caso de falha.
    /// </summary>
    /// <param name="acao">Ação a ser executada.</param>
    /// <returns>O próprio resultado (para encadeamento).</returns>
    public Resultado<T> EmFalha(Action<Erro> acao)
    {
        if (EhFalha)
            acao(Erro);

        return this;
    }

    /// <summary>
    /// Retorna o valor em caso de sucesso, ou um valor padrão em caso de falha.
    /// </summary>
    /// <param name="valorPadrao">Valor padrão a ser retornado em caso de falha.</param>
    /// <returns>O valor ou o valor padrão.</returns>
    public T ValorOu(T valorPadrao) => EhSucesso ? Valor : valorPadrao;

    /// <summary>
    /// Retorna o valor em caso de sucesso, ou executa uma função para obter o valor padrão.
    /// </summary>
    /// <param name="funcaoValorPadrao">Função que retorna o valor padrão.</param>
    /// <returns>O valor ou o resultado da função.</returns>
    public T ValorOu(Func<T> funcaoValorPadrao) => EhSucesso ? Valor : funcaoValorPadrao();

    /// <summary>
    /// Conversão implícita de valor para Resultado de sucesso.
    /// </summary>
    public static implicit operator Resultado<T>(T valor) => Sucesso(valor);

    /// <summary>
    /// Conversão implícita de Erro para Resultado de falha.
    /// </summary>
    public static implicit operator Resultado<T>(Erro erro) => Falha(erro);

    public override string ToString()
    {
        return EhSucesso
            ? $"Sucesso: {Valor}"
            : $"Falha: {Erro}";
    }
}

/// <summary>
/// Classe auxiliar para criar resultados sem valor de retorno (void).
/// </summary>
public static class Resultado
{
    /// <summary>
    /// Cria um resultado de sucesso sem valor.
    /// </summary>
    /// <returns>Resultado de sucesso.</returns>
    public static Resultado<Unit> Sucesso() => Resultado<Unit>.Sucesso(Unit.Valor);

    /// <summary>
    /// Cria um resultado de falha sem valor.
    /// </summary>
    /// <param name="erro">Erro que causou a falha.</param>
    /// <returns>Resultado de falha.</returns>
    public static Resultado<Unit> Falha(Erro erro) => Resultado<Unit>.Falha(erro);

    /// <summary>
    /// Cria um resultado de falha sem valor (mensagem simples).
    /// </summary>
    /// <param name="mensagem">Mensagem de erro.</param>
    /// <returns>Resultado de falha.</returns>
    public static Resultado<Unit> Falha(string mensagem) => Resultado<Unit>.Falha(mensagem);
}

/// <summary>
/// Tipo que representa "sem valor" (equivalente a void em contexto funcional).
/// </summary>
public readonly struct Unit
{
    public static readonly Unit Valor = new();

    public override string ToString() => "()";
}
