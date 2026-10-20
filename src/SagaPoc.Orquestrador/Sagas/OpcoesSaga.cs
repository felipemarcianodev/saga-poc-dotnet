namespace SagaPoc.Orquestrador.Sagas;

/// <summary>
/// Prazos e tentativas da SAGA, lidos da seção "Saga" da configuração.
/// </summary>
public sealed class OpcoesSaga
{
    public const string Secao = "Saga";

    /// <summary>
    /// Tempo máximo para o pedido chegar à notificação. Esgotado, a SAGA compensa o que já foi feito.
    /// </summary>
    public int PrazoPedidoSegundos { get; set; } = 30;

    /// <summary>
    /// Tempo máximo para todas as compensações responderem. Esgotado, os passos sem resposta contam como falha.
    /// </summary>
    public int PrazoCompensacaoSegundos { get; set; } = 30;

    /// <summary>
    /// Quantas vezes cada comando de compensação é enviado antes de o passo contar como falha.
    /// </summary>
    public int MaximoTentativasCompensacao { get; set; } = 3;

    /// <summary>
    /// Espera entre uma tentativa de compensação recusada e a seguinte.
    /// </summary>
    public int IntervaloTentativasSegundos { get; set; } = 2;
}
