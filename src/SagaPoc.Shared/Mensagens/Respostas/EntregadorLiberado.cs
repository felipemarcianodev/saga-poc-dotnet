namespace SagaPoc.Shared.Mensagens.Respostas;

/// <summary>
/// Resposta indicando que um entregador foi liberado.
/// Parte do processo de compensação da SAGA.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Sucesso">Indica se a liberação foi realizada com sucesso.</param>
/// <param name="EntregadorId">Identificador do entregador liberado.</param>
public record EntregadorLiberado(
    Guid CorrelacaoId,
    bool Sucesso,
    string EntregadorId
);
