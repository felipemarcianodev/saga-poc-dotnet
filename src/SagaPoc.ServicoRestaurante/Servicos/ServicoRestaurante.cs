using SagaPoc.Common.Modelos;
using SagaPoc.Common.ResultPattern;
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

    /// <summary>
    /// Valida um pedido usando encadeamento de validações com Result Pattern.
    /// Demonstra o uso de Bind/BindAsync para Railway-Oriented Programming.
    /// </summary>
    public async Task<Resultado<DadosValidacaoPedido>> ValidarPedidoAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        _logger.LogInformation(
            "Validando pedido no restaurante {RestauranteId} com {QuantidadeItens} itens",
            restauranteId,
            itens.Count
        );

        // Validação em cascata usando Bind/BindAsync (Railway-Oriented Programming)
        // 1. Validar restaurante
        var resultadoValidacao = await ValidarRestauranteAsync(restauranteId);
        if (resultadoValidacao.EhFalha)
            return Resultado<DadosValidacaoPedido>.Falha(resultadoValidacao.Erro);

        // 2. Validar itens (encadeamento com BindAsync)
        var resultadoItens = await resultadoValidacao
            .BindAsync(_ => ValidarItensAsync(restauranteId, itens));

        if (resultadoItens.EhFalha)
            return Resultado<DadosValidacaoPedido>.Falha(resultadoItens.Erro);

        // 3. Calcular valores finais (encadeamento com BindAsync)
        return await resultadoItens
            .BindAsync(itensValidados => CalcularValorTotalAsync(restauranteId, itensValidados));
    }

    /// <summary>
    /// Etapa 1: Valida se o restaurante existe e está aberto.
    /// </summary>
    private async Task<Resultado<bool>> ValidarRestauranteAsync(string restauranteId)
    {
        // Simulação de delay de consulta ao banco
        await Task.Delay(Random.Shared.Next(50, 150));

        return restauranteId switch
        {
            "REST_FECHADO" => Resultado<bool>.Falha(
                Erro.Negocio("RESTAURANTE_FECHADO", "Restaurante está fechado no momento")
            ),
            "REST_INVALIDO" => Resultado<bool>.Falha(
                Erro.NaoEncontrado($"Restaurante {restauranteId} não encontrado")
            ),
            "REST_INATIVO" => Resultado<bool>.Falha(
                Erro.Negocio("RESTAURANTE_INATIVO", "Restaurante temporariamente indisponível")
            ),
            _ when restauranteId.StartsWith("REST") => Resultado<bool>.Sucesso(true),
            _ => Resultado<bool>.Falha(
                Erro.NaoEncontrado($"Restaurante '{restauranteId}' não encontrado", "RESTAURANTE_NAO_ENCONTRADO")
            )
        };
    }

    /// <summary>
    /// Etapa 2: Valida se todos os itens estão disponíveis.
    /// </summary>
    private async Task<Resultado<List<ItemPedido>>> ValidarItensAsync(
        string restauranteId,
        List<ItemPedido> itens)
    {
        // Simulação de delay de consulta ao estoque
        await Task.Delay(Random.Shared.Next(100, 300));

        var erros = new List<Erro>();

        foreach (var item in itens)
        {
            // Validação de item indisponível
            if (item.ProdutoId == "INDISPONIVEL")
            {
                erros.Add(Erro.Negocio(
                    "PRODUTO_INDISPONIVEL",
                    $"Produto '{item.Nome}' está indisponível"
                ));
            }

            // Validação de quantidade
            if (item.Quantidade > 10)
            {
                erros.Add(Erro.Validacao(
                    "QUANTIDADE_EXCEDIDA",
                    $"Quantidade máxima para '{item.Nome}' é 10 unidades"
                ));
            }

            // Validação de preço
            if (item.PrecoUnitario <= 0)
            {
                erros.Add(Erro.Validacao(
                    "PRECO_INVALIDO",
                    $"Preço do item '{item.Nome}' é inválido"
                ));
            }
        }

        // Se houver erros, retornar todos eles
        if (erros.Any())
        {
            _logger.LogWarning(
                "Itens com problemas no restaurante {RestauranteId}: {Erros}",
                restauranteId,
                string.Join("; ", erros.Select(e => e.Mensagem))
            );
            return Resultado<List<ItemPedido>>.Falha(erros);
        }

        return Resultado<List<ItemPedido>>.Sucesso(itens);
    }

    /// <summary>
    /// Etapa 3: Calcula o valor total e tempo de preparo.
    /// </summary>
    private async Task<Resultado<DadosValidacaoPedido>> CalcularValorTotalAsync(
        string restauranteId,
        List<ItemPedido> itensValidados)
    {
        // Simulação de delay de cálculo
        await Task.Delay(Random.Shared.Next(50, 100));

        var valorTotal = itensValidados.Sum(i => i.PrecoUnitario * i.Quantidade);

        // Validar se o valor mínimo foi atingido
        if (valorTotal < 10m)
        {
            return Resultado<DadosValidacaoPedido>.Falha(
                Erro.Validacao("VALOR_MINIMO", "Valor mínimo do pedido é R$ 10,00")
            );
        }

        // Tempo de preparo baseado na quantidade de itens (15 min por item)
        // Restaurantes VIP têm preparo mais rápido (metade do tempo)
        var tempoPreparoBase = itensValidados.Count * 15;
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
