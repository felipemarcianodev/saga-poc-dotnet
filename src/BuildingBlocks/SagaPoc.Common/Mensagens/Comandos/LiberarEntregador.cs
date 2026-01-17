namespace SagaPoc.Common.Mensagens.Comandos;

/// <summary>
/// Comando de compensação para liberar um entregador alocado.
/// Executado quando a SAGA falha após alocar o entregador.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="EntregadorId">Identificador do entregador a ser liberado.</param>
public record LiberarEntregador(
    Guid CorrelacaoId,
    string EntregadorId
);
