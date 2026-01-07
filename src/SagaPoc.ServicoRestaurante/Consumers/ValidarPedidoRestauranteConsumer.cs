using MassTransit;
using SagaPoc.Shared.Mensagens.Comandos;
using SagaPoc.Shared.Mensagens.Respostas;
using SagaPoc.ServicoRestaurante.Servicos;

namespace SagaPoc.ServicoRestaurante.Consumers;

/// <summary>
/// Consumer responsável por validar pedidos de restaurante.
/// Recebe comando ValidarPedidoRestaurante e responde com PedidoRestauranteValidado.
/// </summary>
public class ValidarPedidoRestauranteConsumer : IConsumer<ValidarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly ILogger<ValidarPedidoRestauranteConsumer> _logger;

    public ValidarPedidoRestauranteConsumer(
        IServicoRestaurante servico,
        ILogger<ValidarPedidoRestauranteConsumer> logger)
    {
        _servico = servico;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ValidarPedidoRestaurante> context)
    {
        var mensagem = context.Message;

        _logger.LogInformation(
            "Recebido comando ValidarPedidoRestaurante. CorrelacaoId: {CorrelacaoId}, " +
            "RestauranteId: {RestauranteId}, QuantidadeItens: {QuantidadeItens}",
            mensagem.CorrelacaoId,
            mensagem.RestauranteId,
            mensagem.Itens.Count
        );

        try
        {
            // Executar validação do pedido
            var resultado = await _servico.ValidarPedidoAsync(
                mensagem.RestauranteId,
                mensagem.Itens
            );

            // Usar Match() para tratar sucesso/falha de forma elegante (Result Pattern)
            resultado.Match(
                sucesso: dados =>
                {
                    _logger.LogInformation(
                        "[Restaurante] Pedido {CorrelacaoId} validado com sucesso - " +
                        "Valor: R$ {Valor:F2}, Tempo: {Tempo}min, PedidoId: {PedidoId}",
                        mensagem.CorrelacaoId,
                        dados.ValorTotal,
                        dados.TempoPreparoMinutos,
                        dados.PedidoId
                    );
                },
                falha: erro =>
                {
                    _logger.LogWarning(
                        "[Restaurante] Pedido {CorrelacaoId} rejeitado - " +
                        "TipoErro: {TipoErro}, Codigo: {Codigo}, Motivo: {Motivo}",
                        mensagem.CorrelacaoId,
                        erro.Tipo,
                        erro.Codigo,
                        erro.Mensagem
                    );
                }
            );

            // Preparar resposta baseada no resultado
            var resposta = resultado.Match(
                sucesso: dados => new PedidoRestauranteValidado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Valido: true,
                    ValorTotal: dados.ValorTotal,
                    TempoPreparoMinutos: dados.TempoPreparoMinutos,
                    PedidoId: dados.PedidoId,
                    MotivoRejeicao: null
                ),
                falha: erro => new PedidoRestauranteValidado(
                    CorrelacaoId: mensagem.CorrelacaoId,
                    Valido: false,
                    ValorTotal: 0,
                    TempoPreparoMinutos: 0,
                    PedidoId: null,
                    MotivoRejeicao: $"[{erro.Codigo}] {erro.Mensagem}"
                )
            );

            // Enviar resposta
            await context.RespondAsync(resposta);

            _logger.LogInformation(
                "Resposta enviada. CorrelacaoId: {CorrelacaoId}, Valido: {Valido}",
                mensagem.CorrelacaoId,
                resposta.Valido
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Erro ao processar ValidarPedidoRestaurante. CorrelacaoId: {CorrelacaoId}",
                mensagem.CorrelacaoId
            );

            // Enviar resposta de falha em caso de exceção inesperada
            await context.RespondAsync(new PedidoRestauranteValidado(
                CorrelacaoId: mensagem.CorrelacaoId,
                Valido: false,
                ValorTotal: 0,
                TempoPreparoMinutos: 0,
                PedidoId: null,
                MotivoRejeicao: "Erro interno ao validar pedido no restaurante"
            ));

            throw; // Re-throw para MassTransit lidar com retry policy
        }
    }
}
