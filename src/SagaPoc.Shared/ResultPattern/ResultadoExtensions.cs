namespace SagaPoc.Shared.ResultPattern;

/// <summary>
/// Métodos de extensão para facilitar o uso do padrão Result.
/// </summary>
public static class ResultadoExtensions
{
    /// <summary>
    /// Converte uma Task de valor em uma Task de Resultado.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="task">Task contendo o valor.</param>
    /// <returns>Task contendo o Resultado de sucesso.</returns>
    public static async Task<Resultado<T>> ParaResultado<T>(this Task<T> task)
    {
        var valor = await task;
        return Resultado<T>.Sucesso(valor);
    }

    /// <summary>
    /// Combina uma lista de resultados em um único resultado.
    /// Se todos forem sucesso, retorna lista de valores.
    /// Se algum falhar, retorna o primeiro erro.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultados">Lista de resultados.</param>
    /// <returns>Resultado combinado.</returns>
    public static Resultado<List<T>> Combinar<T>(this IEnumerable<Resultado<T>> resultados)
    {
        var lista = resultados.ToList();

        var primeiraFalha = lista.FirstOrDefault(r => r.EhFalha);
        if (primeiraFalha != null)
            return Resultado<List<T>>.Falha(primeiraFalha.Erro);

        var valores = lista.Select(r => r.Valor).ToList();
        return Resultado<List<T>>.Sucesso(valores);
    }

    /// <summary>
    /// Combina uma lista de resultados coletando todos os erros.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultados">Lista de resultados.</param>
    /// <returns>Resultado com todos os valores ou todos os erros.</returns>
    public static Resultado<List<T>> CombinarTodosErros<T>(this IEnumerable<Resultado<T>> resultados)
    {
        var lista = resultados.ToList();

        var erros = lista.Where(r => r.EhFalha).SelectMany(r => r.Erros).ToList();

        if (erros.Any())
            return Resultado<List<T>>.Falha(erros);

        var valores = lista.Select(r => r.Valor).ToList();
        return Resultado<List<T>>.Sucesso(valores);
    }

    /// <summary>
    /// Executa uma função que pode lançar exceção e encapsula em um Resultado.
    /// </summary>
    /// <typeparam name="T">Tipo do valor de retorno.</typeparam>
    /// <param name="funcao">Função a ser executada.</param>
    /// <returns>Resultado de sucesso ou falha técnica.</returns>
    public static Resultado<T> Tentar<T>(Func<T> funcao)
    {
        try
        {
            return Resultado<T>.Sucesso(funcao());
        }
        catch (Exception ex)
        {
            return Resultado<T>.Falha(
                Erro.Tecnico(
                    "EXCECAO_NAO_TRATADA",
                    ex.Message,
                    new Dictionary<string, object>
                    {
                        ["TipoExcecao"] = ex.GetType().Name,
                        ["StackTrace"] = ex.StackTrace ?? string.Empty
                    }
                )
            );
        }
    }

    /// <summary>
    /// Executa uma função assíncrona que pode lançar exceção e encapsula em um Resultado.
    /// </summary>
    /// <typeparam name="T">Tipo do valor de retorno.</typeparam>
    /// <param name="funcao">Função assíncrona a ser executada.</param>
    /// <returns>Task contendo o Resultado.</returns>
    public static async Task<Resultado<T>> TentarAsync<T>(Func<Task<T>> funcao)
    {
        try
        {
            var valor = await funcao();
            return Resultado<T>.Sucesso(valor);
        }
        catch (Exception ex)
        {
            return Resultado<T>.Falha(
                Erro.Tecnico(
                    "EXCECAO_NAO_TRATADA",
                    ex.Message,
                    new Dictionary<string, object>
                    {
                        ["TipoExcecao"] = ex.GetType().Name,
                        ["StackTrace"] = ex.StackTrace ?? string.Empty
                    }
                )
            );
        }
    }

    /// <summary>
    /// Garante que uma condição seja verdadeira, caso contrário retorna erro.
    /// </summary>
    /// <param name="resultado">Resultado atual.</param>
    /// <param name="predicado">Condição a ser verificada.</param>
    /// <param name="erro">Erro a ser retornado se a condição for falsa.</param>
    /// <returns>Resultado original ou erro.</returns>
    public static Resultado<T> Garantir<T>(
        this Resultado<T> resultado,
        Func<T, bool> predicado,
        Erro erro)
    {
        if (resultado.EhFalha)
            return resultado;

        return predicado(resultado.Valor)
            ? resultado
            : Resultado<T>.Falha(erro);
    }

    /// <summary>
    /// Valida o resultado usando uma lista de validadores.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultado">Resultado a ser validado.</param>
    /// <param name="validadores">Lista de funções de validação.</param>
    /// <returns>Resultado validado ou com erros.</returns>
    public static Resultado<T> Validar<T>(
        this Resultado<T> resultado,
        params Func<T, Resultado<T>>[] validadores)
    {
        if (resultado.EhFalha)
            return resultado;

        foreach (var validador in validadores)
        {
            resultado = validador(resultado.Valor);
            if (resultado.EhFalha)
                return resultado;
        }

        return resultado;
    }

    /// <summary>
    /// Converte um Resultado em outro tipo usando uma função de conversão.
    /// </summary>
    /// <typeparam name="TIn">Tipo de entrada.</typeparam>
    /// <typeparam name="TOut">Tipo de saída.</typeparam>
    /// <param name="resultado">Resultado de entrada.</param>
    /// <param name="conversor">Função de conversão.</param>
    /// <returns>Resultado convertido.</returns>
    public static Resultado<TOut> Converter<TIn, TOut>(
        this Resultado<TIn> resultado,
        Func<TIn, TOut> conversor)
    {
        return resultado.Map(conversor);
    }

    /// <summary>
    /// Registra logs do resultado (útil para debugging e monitoramento).
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultado">Resultado a ser logado.</param>
    /// <param name="acaoSucesso">Ação de log em caso de sucesso.</param>
    /// <param name="acaoFalha">Ação de log em caso de falha.</param>
    /// <returns>O mesmo resultado (para encadeamento).</returns>
    public static Resultado<T> RegistrarLog<T>(
        this Resultado<T> resultado,
        Action<T>? acaoSucesso = null,
        Action<Erro>? acaoFalha = null)
    {
        if (resultado.EhSucesso && acaoSucesso != null)
            acaoSucesso(resultado.Valor);

        if (resultado.EhFalha && acaoFalha != null)
            acaoFalha(resultado.Erro);

        return resultado;
    }

    /// <summary>
    /// Executa uma ação com o valor do resultado, sem modificá-lo.
    /// Útil para side effects como logging.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultado">Resultado.</param>
    /// <param name="acao">Ação a ser executada.</param>
    /// <returns>O mesmo resultado.</returns>
    public static Resultado<T> Tap<T>(this Resultado<T> resultado, Action<T> acao)
    {
        if (resultado.EhSucesso)
            acao(resultado.Valor);

        return resultado;
    }

    /// <summary>
    /// Executa uma ação assíncrona com o valor do resultado, sem modificá-lo.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultado">Resultado.</param>
    /// <param name="acao">Ação assíncrona a ser executada.</param>
    /// <returns>Task contendo o mesmo resultado.</returns>
    public static async Task<Resultado<T>> TapAsync<T>(
        this Resultado<T> resultado,
        Func<T, Task> acao)
    {
        if (resultado.EhSucesso)
            await acao(resultado.Valor);

        return resultado;
    }

    /// <summary>
    /// Filtra o valor do resultado baseado em um predicado.
    /// </summary>
    /// <typeparam name="T">Tipo do valor.</typeparam>
    /// <param name="resultado">Resultado.</param>
    /// <param name="predicado">Condição de filtro.</param>
    /// <param name="erroSeFalhar">Erro a retornar se o predicado for falso.</param>
    /// <returns>Resultado filtrado.</returns>
    public static Resultado<T> Filtrar<T>(
        this Resultado<T> resultado,
        Func<T, bool> predicado,
        Erro erroSeFalhar)
    {
        if (resultado.EhFalha)
            return resultado;

        return predicado(resultado.Valor)
            ? resultado
            : Resultado<T>.Falha(erroSeFalhar);
    }
}
