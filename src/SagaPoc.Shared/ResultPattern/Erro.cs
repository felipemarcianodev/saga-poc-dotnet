namespace SagaPoc.Shared.ResultPattern;

/// <summary>
/// Representa um erro que ocorreu durante a execução de uma operação.
/// </summary>
public sealed class Erro
{
    /// <summary>
    /// Obtém o código do erro.
    /// </summary>
    public string Codigo { get; }

    /// <summary>
    /// Obtém a mensagem descritiva do erro.
    /// </summary>
    public string Mensagem { get; }

    /// <summary>
    /// Obtém os detalhes adicionais do erro (opcional).
    /// </summary>
    public Dictionary<string, object>? Detalhes { get; }

    /// <summary>
    /// Obtém o tipo/categoria do erro.
    /// </summary>
    public TipoErro Tipo { get; }

    private Erro(string codigo, string mensagem, TipoErro tipo, Dictionary<string, object>? detalhes = null)
    {
        Codigo = codigo;
        Mensagem = mensagem;
        Tipo = tipo;
        Detalhes = detalhes;
    }

    /// <summary>
    /// Cria um erro de validação.
    /// </summary>
    /// <param name="codigo">Código do erro.</param>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="detalhes">Detalhes adicionais (opcional).</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Validacao(string codigo, string mensagem, Dictionary<string, object>? detalhes = null) =>
        new(codigo, mensagem, TipoErro.Validacao, detalhes);

    /// <summary>
    /// Cria um erro de validação com mensagem apenas.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Validacao(string mensagem) =>
        new("ERRO_VALIDACAO", mensagem, TipoErro.Validacao);

    /// <summary>
    /// Cria um erro de negócio.
    /// </summary>
    /// <param name="codigo">Código do erro.</param>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="detalhes">Detalhes adicionais (opcional).</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Negocio(string codigo, string mensagem, Dictionary<string, object>? detalhes = null) =>
        new(codigo, mensagem, TipoErro.Negocio, detalhes);

    /// <summary>
    /// Cria um erro de negócio com mensagem apenas.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Negocio(string mensagem) =>
        new("ERRO_NEGOCIO", mensagem, TipoErro.Negocio);

    /// <summary>
    /// Cria um erro técnico/infraestrutura.
    /// </summary>
    /// <param name="codigo">Código do erro.</param>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="detalhes">Detalhes adicionais (opcional).</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Tecnico(string codigo, string mensagem, Dictionary<string, object>? detalhes = null) =>
        new(codigo, mensagem, TipoErro.Tecnico, detalhes);

    /// <summary>
    /// Cria um erro técnico com mensagem apenas.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Tecnico(string mensagem) =>
        new("ERRO_TECNICO", mensagem, TipoErro.Tecnico);

    /// <summary>
    /// Cria um erro de recurso não encontrado.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro NaoEncontrado(string mensagem) =>
        new("NAO_ENCONTRADO", mensagem, TipoErro.NaoEncontrado);

    /// <summary>
    /// Cria um erro de conflito.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Conflito(string mensagem) =>
        new("CONFLITO", mensagem, TipoErro.Conflito);

    /// <summary>
    /// Cria um erro de timeout.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="codigo">Código do erro (opcional).</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Timeout(string mensagem, string codigo = "TIMEOUT") =>
        new(codigo, mensagem, TipoErro.Timeout);

    /// <summary>
    /// Cria um erro de infraestrutura.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="codigo">Código do erro (opcional).</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Infraestrutura(string mensagem, string codigo = "INFRAESTRUTURA") =>
        new(codigo, mensagem, TipoErro.Infraestrutura);

    /// <summary>
    /// Cria um erro externo (de API/serviço externo).
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="codigo">Código do erro (opcional).</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro Externo(string mensagem, string codigo = "EXTERNO") =>
        new(codigo, mensagem, TipoErro.Externo);

    /// <summary>
    /// Cria um erro de recurso não encontrado com código customizado.
    /// </summary>
    /// <param name="mensagem">Mensagem descritiva.</param>
    /// <param name="codigo">Código do erro.</param>
    /// <returns>Instância de <see cref="Erro"/>.</returns>
    public static Erro NaoEncontrado(string mensagem, string codigo) =>
        new(codigo, mensagem, TipoErro.NaoEncontrado);

    public override string ToString() => $"[{Tipo}] {Codigo}: {Mensagem}";
}

/// <summary>
/// Tipos/categorias de erro.
/// </summary>
public enum TipoErro
{
    /// <summary>
    /// Erro de validação de entrada (dados inválidos, campos obrigatórios, etc).
    /// </summary>
    Validacao,

    /// <summary>
    /// Erro de regra de negócio (ex: restaurante fechado, saldo insuficiente).
    /// </summary>
    Negocio,

    /// <summary>
    /// Erro técnico/infraestrutura (ex: falha de conexão, timeout).
    /// </summary>
    Tecnico,

    /// <summary>
    /// Recurso não encontrado.
    /// </summary>
    NaoEncontrado,

    /// <summary>
    /// Conflito (ex: recurso já existe, estado inconsistente).
    /// </summary>
    Conflito,

    /// <summary>
    /// Timeout em operação (ex: gateway de pagamento não respondeu).
    /// </summary>
    Timeout,

    /// <summary>
    /// Erro de infraestrutura (ex: banco de dados, fila de mensagens).
    /// </summary>
    Infraestrutura,

    /// <summary>
    /// Erro de sistema externo (ex: API externa retornou erro).
    /// </summary>
    Externo
}
