namespace SagaPoc.Common.Mensagens.Respostas;

/// <summary>
/// Resposta da alocação de entregador.
/// </summary>
/// <param name="CorrelacaoId">ID de correlação da SAGA.</param>
/// <param name="Alocado">Indica se um entregador foi alocado.</param>
/// <param name="EntregadorId">Identificador do entregador alocado (se sucesso).</param>
/// <param name="TempoEstimadoMinutos">Tempo estimado de entrega em minutos.</param>
/// <param name="MotivoFalha">Motivo da falha (se não alocado).</param>
public record EntregadorAlocado(
    Guid CorrelacaoId,
    bool Alocado,
    string? EntregadorId,
    int TempoEstimadoMinutos,
    string? MotivoFalha
);
