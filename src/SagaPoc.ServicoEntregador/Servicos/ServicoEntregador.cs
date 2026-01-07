using SagaPoc.Shared.ResultPattern;
using SagaPoc.ServicoEntregador.Modelos;

namespace SagaPoc.ServicoEntregador.Servicos;

/// <summary>
/// Implementação do serviço de alocação de entregadores.
/// NOTA: Esta é uma implementação simulada para fins de POC.
/// Em produção, isso integraria com um sistema de geolocalização e gestão de entregadores.
/// </summary>
public class ServicoEntregador : IServicoEntregador
{
    private readonly ILogger<ServicoEntregador> _logger;

    // Simulação de entregadores em memória (apenas para POC)
    private static readonly List<string> EntregadoresDisponiveis = new()
    {
        "ENT001", "ENT002", "ENT003", "ENT004", "ENT005"
    };

    private static readonly HashSet<string> EntregadoresAlocados = new();
    private static readonly object Lock = new();

    public ServicoEntregador(ILogger<ServicoEntregador> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Aloca um entregador usando validação em cascata com Result Pattern.
    /// </summary>
    public async Task<Resultado<DadosAlocacao>> AlocarAsync(
        string restauranteId,
        string enderecoEntrega,
        decimal taxaEntrega)
    {
        _logger.LogInformation(
            "Alocando entregador. RestauranteId: {RestauranteId}, " +
            "Endereco: {Endereco}, TaxaEntrega: {Taxa:C}",
            restauranteId,
            enderecoEntrega,
            taxaEntrega
        );

        // Validação em cascata (Railway-Oriented Programming)
        // 1. Validar taxa de entrega
        var resultadoTaxa = ValidarTaxaEntrega(taxaEntrega);
        if (resultadoTaxa.EhFalha)
            return Resultado<DadosAlocacao>.Falha(resultadoTaxa.Erro);

        // 2. Validar endereço (encadeamento com BindAsync)
        var resultadoEndereco = await resultadoTaxa
            .BindAsync(_ => ValidarEnderecoAsync(enderecoEntrega));

        if (resultadoEndereco.EhFalha)
            return Resultado<DadosAlocacao>.Falha(resultadoEndereco.Erro);

        // 3. Buscar e alocar entregador (encadeamento com BindAsync)
        return await resultadoEndereco
            .BindAsync(_ => BuscarEAlocarEntregadorAsync(restauranteId, enderecoEntrega, taxaEntrega));
    }

    /// <summary>
    /// Etapa 1: Valida a taxa de entrega.
    /// </summary>
    private Resultado<Unit> ValidarTaxaEntrega(decimal taxaEntrega)
    {
        if (taxaEntrega < 0)
        {
            return Resultado.Falha(
                Erro.Validacao("TAXA_NEGATIVA", "Taxa de entrega não pode ser negativa")
            );
        }

        if (taxaEntrega > 100m)
        {
            return Resultado.Falha(
                Erro.Validacao("TAXA_MUITO_ALTA", "Taxa de entrega excede o limite máximo (R$ 100,00)")
            );
        }

        return Resultado.Sucesso();
    }

    /// <summary>
    /// Etapa 2: Valida o endereço de entrega.
    /// </summary>
    private async Task<Resultado<Unit>> ValidarEnderecoAsync(string enderecoEntrega)
    {
        // Simulação de delay de validação de endereço (chamada a API de geolocalização)
        await Task.Delay(Random.Shared.Next(50, 150));

        if (string.IsNullOrWhiteSpace(enderecoEntrega))
        {
            return Resultado.Falha(
                Erro.Validacao("ENDERECO_VAZIO", "Endereço de entrega é obrigatório")
            );
        }

        if (enderecoEntrega.Length < 10)
        {
            return Resultado.Falha(
                Erro.Validacao("ENDERECO_INVALIDO", "Endereço de entrega muito curto")
            );
        }

        // Validar se está dentro da área de cobertura
        if (enderecoEntrega.Contains("LONGE") || enderecoEntrega.Contains("DISTANTE"))
        {
            return Resultado.Falha(
                Erro.Negocio("AREA_NAO_COBERTA", "Endereço fora da área de cobertura")
            );
        }

        return Resultado.Sucesso();
    }

    /// <summary>
    /// Etapa 3: Busca e aloca um entregador disponível.
    /// </summary>
    private async Task<Resultado<DadosAlocacao>> BuscarEAlocarEntregadorAsync(
        string restauranteId,
        string enderecoEntrega,
        decimal taxaEntrega)
    {
        // Simulação de delay de busca de entregador
        await Task.Delay(Random.Shared.Next(150, 400));

        // Verificar horário de pico
        if (DateTime.UtcNow.Hour >= 12 && DateTime.UtcNow.Hour <= 14)
        {
            // Simular probabilidade de não ter entregador (30% de chance)
            if (Random.Shared.Next(100) < 30)
            {
                _logger.LogWarning("Nenhum entregador disponível (horário de pico)");
                return Resultado<DadosAlocacao>.Falha(
                    Erro.Negocio(
                        "HORARIO_PICO",
                        "Todos os entregadores estão ocupados. Tente novamente em alguns minutos."
                    )
                );
            }
        }

        // Tentar alocar entregador
        string? entregadorId = null;
        lock (Lock)
        {
            entregadorId = EntregadoresDisponiveis
                .FirstOrDefault(e => !EntregadoresAlocados.Contains(e));

            if (entregadorId == null)
            {
                _logger.LogWarning("Todos os entregadores estão alocados no momento");
                return Resultado<DadosAlocacao>.Falha(
                    Erro.Negocio(
                        "SEM_ENTREGADOR_DISPONIVEL",
                        "Todos os entregadores estão ocupados no momento"
                    )
                );
            }

            EntregadoresAlocados.Add(entregadorId);
        }

        // Calcular distância e tempo estimado (simulado)
        var distanciaKm = Random.Shared.Next(1, 15) + (decimal)Random.Shared.NextDouble();
        var tempoEstimado = (int)(distanciaKm * 3) + Random.Shared.Next(5, 15); // ~3 min por km + tempo base

        // Entregadores VIP/Prioritários são mais rápidos
        if (restauranteId == "REST_VIP")
        {
            tempoEstimado = (int)(tempoEstimado * 0.7); // 30% mais rápido
        }

        _logger.LogInformation(
            "Entregador alocado. EntregadorId: {EntregadorId}, RestauranteId: {RestauranteId}, " +
            "Distancia: {Distancia:F2}km, TempoEstimado: {Tempo}min",
            entregadorId,
            restauranteId,
            distanciaKm,
            tempoEstimado
        );

        return Resultado<DadosAlocacao>.Sucesso(
            new DadosAlocacao(
                EntregadorId: entregadorId,
                NomeEntregador: $"Entregador {entregadorId}",
                TempoEstimadoMinutos: tempoEstimado,
                DistanciaKm: distanciaKm
            )
        );
    }

    public async Task<Resultado<Unit>> LiberarAsync(string entregadorId)
    {
        _logger.LogWarning(
            "COMPENSAÇÃO: Liberando entregador. EntregadorId: {EntregadorId}",
            entregadorId
        );

        // Simulação de delay de processamento
        await Task.Delay(Random.Shared.Next(50, 200));

        lock (Lock)
        {
            if (!EntregadoresAlocados.Contains(entregadorId))
            {
                _logger.LogWarning(
                    "COMPENSAÇÃO: Entregador já estava livre (idempotência). EntregadorId: {EntregadorId}",
                    entregadorId
                );
                // Retornar sucesso para garantir idempotência
                return Resultado.Sucesso();
            }

            EntregadoresAlocados.Remove(entregadorId);
        }

        _logger.LogInformation(
            "COMPENSAÇÃO: Entregador liberado com sucesso. EntregadorId: {EntregadorId}",
            entregadorId
        );

        return Resultado.Sucesso();
    }
}
