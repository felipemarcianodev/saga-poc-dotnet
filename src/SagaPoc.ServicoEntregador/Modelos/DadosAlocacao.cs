namespace SagaPoc.ServicoEntregador.Modelos;

/// <summary>
/// Dados de alocação de um entregador.
/// </summary>
/// <param name="EntregadorId">Identificador do entregador alocado.</param>
/// <param name="NomeEntregador">Nome do entregador.</param>
/// <param name="TempoEstimadoMinutos">Tempo estimado de entrega em minutos.</param>
/// <param name="DistanciaKm">Distância até o endereço de entrega em km.</param>
public record DadosAlocacao(
    string EntregadorId,
    string NomeEntregador,
    int TempoEstimadoMinutos,
    decimal DistanciaKm
);
