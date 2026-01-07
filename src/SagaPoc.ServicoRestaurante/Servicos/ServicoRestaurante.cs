using SagaPoc.Shared.Modelos;
using SagaPoc.Shared.ResultPattern;
using SagaPoc.ServicoRestaurante.Modelos;

namespace SagaPoc.ServicoRestaurante.Servicos;

/// <summary>
/// Implementação do serviço de validação de pedidos de restaurante.
/// NOTA: Esta é uma implementação simulada para fins de POC.
/// Em produção, isso faria chamadas a um banco de dados real ou API externa.
/// </summary>
public class ServicoRestaurante : IServicoRestaurante
{
    private readonly ILogger<ServicoRestaurante> _logger;

    // Simulação de banco de dados em memória (apenas para POC)
    private static readonly Dictionary<Guid, (string RestauranteId, DateTime DataCancelamento)> PedidosCancelados = new();

    public ServicoRestaurante(ILogger<ServicoRestaurante> logger)
    {
        _logger = logger;
    }

    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        _logger.LogInformation(
            "Validando pedido no restaurante {RestauranteId} com {QuantidadeItens} itens",
            restauranteId,
            itens.Count
        );

        // Simulação de delay de processamento (simula chamada a banco/API)
        await Task.Delay(Random.Shared.Next(100, 500));

        // CENÁRIO 1: Restaurante fechado
        if (restauranteId == "REST_FECHADO")
        {
            _logger.LogWarning("Restaurante {RestauranteId} está fechado", restauranteId);
            return Resultado<DadosValidacaoPedido>.Falha(
                Erro.Negocio("RESTAURANTE_FECHADO", "Restaurante fechado no momento")
            );
        }

        // CENÁRIO 2: Restaurante inexistente
        if (restauranteId == "REST_INVALIDO")
        {
            _logger.LogWarning("Restaurante {RestauranteId} não encontrado", restauranteId);
            return Resultado<DadosValidacaoPedido>.Falha(
                Erro.NaoEncontrado($"Restaurante {restauranteId} não encontrado")
            );
        }

        // CENÁRIO 3: Item indisponível
        var itemIndisponivel = itens.FirstOrDefault(i => i.ProdutoId == "INDISPONIVEL");
        if (itemIndisponivel != null)
        {
            _logger.LogWarning(
                "Item {ProdutoId} indisponível no restaurante {RestauranteId}",
                itemIndisponivel.ProdutoId,
                restauranteId
            );
            return Resultado<DadosValidacaoPedido>.Falha(
                Erro.Negocio(
                    "ITEM_INDISPONIVEL",
                    $"O item '{itemIndisponivel.Nome}' não está disponível no momento"
                )
            );
        }

        // CENÁRIO 4: Validação OK - Calcular valores
        var valorTotal = itens.Sum(i => i.PrecoUnitario * i.Quantidade);

        // Tempo de preparo baseado na quantidade de itens (10 min por item)
        // Restaurantes VIP têm preparo mais rápido
        var tempoPreparoBase = itens.Count * 10;
        var tempoPreparo = restauranteId == "REST_VIP"
            ? tempoPreparoBase / 2
            : tempoPreparoBase;

        var pedidoId = Guid.NewGuid();

        _logger.LogInformation(
            "Pedido validado com sucesso. RestauranteId: {RestauranteId}, PedidoId: {PedidoId}, " +
            "ValorTotal: {ValorTotal:C}, TempoPreparo: {TempoPreparo}min",
            restauranteId,
            pedidoId,
            valorTotal,
            tempoPreparo
        );

        return Resultado<DadosValidacaoPedido>.Sucesso(
            new DadosValidacaoPedido(
                ValorTotal: valorTotal,
                TempoPreparoMinutos: tempoPreparo,
                PedidoId: pedidoId
            )
        );
    }

    public async Task<Resultado<Unit>> CancelarPedidoAsync(
        string restauranteId,
        Guid pedidoId)
    {
        _logger.LogWarning(
            "COMPENSAÇÃO: Cancelando pedido {PedidoId} no restaurante {RestauranteId}",
            pedidoId,
            restauranteId
        );

        // Simulação de delay de processamento
        await Task.Delay(Random.Shared.Next(50, 200));

        // Registrar cancelamento (em produção, isso atualizaria o banco de dados)
        PedidosCancelados[pedidoId] = (restauranteId, DateTime.UtcNow);

        _logger.LogInformation(
            "Pedido {PedidoId} cancelado com sucesso no restaurante {RestauranteId}",
            pedidoId,
            restauranteId
        );

        return Resultado.Sucesso();
    }
}
