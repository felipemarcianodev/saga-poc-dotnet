using SagaPoc.Shared.ResultPattern;
using SagaPoc.ServicoPagamento.Modelos;

namespace SagaPoc.ServicoPagamento.Servicos;

/// <summary>
/// Implementação do serviço de processamento de pagamentos.
/// NOTA: Esta é uma implementação simulada para fins de POC.
/// Em produção, isso integraria com um gateway de pagamento real (Stripe, PagSeguro, etc).
/// </summary>
public class ServicoPagamento : IServicoPagamento
{
    private readonly ILogger<ServicoPagamento> _logger;

    // Simulação de banco de dados em memória (apenas para POC)
    private static readonly Dictionary<string, (string ClienteId, decimal Valor, DateTime Data, bool Estornado)> Transacoes = new();

    public ServicoPagamento(ILogger<ServicoPagamento> logger)
    {
        _logger = logger;
    }

    public async Task<Resultado<DadosTransacao>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento)
    {
        _logger.LogInformation(
            "Processando pagamento. ClienteId: {ClienteId}, Valor: {Valor:C}, FormaPagamento: {FormaPagamento}",
            clienteId,
            valorTotal,
            formaPagamento
        );

        // Simulação de delay de processamento (integração com gateway)
        await Task.Delay(Random.Shared.Next(200, 800));

        // CENÁRIO 1: Cliente com cartão recusado
        if (clienteId == "CLI_CARTAO_RECUSADO")
        {
            _logger.LogWarning(
                "Pagamento recusado. ClienteId: {ClienteId}, Motivo: Cartão recusado",
                clienteId
            );
            return Resultado<DadosTransacao>.Falha(
                Erro.Negocio("CARTAO_RECUSADO", "Cartão de crédito recusado pela operadora")
            );
        }

        // CENÁRIO 2: Saldo insuficiente
        if (clienteId == "CLI_SALDO_INSUFICIENTE")
        {
            _logger.LogWarning(
                "Pagamento recusado. ClienteId: {ClienteId}, Motivo: Saldo insuficiente",
                clienteId
            );
            return Resultado<DadosTransacao>.Falha(
                Erro.Negocio("SALDO_INSUFICIENTE", "Saldo insuficiente para completar a transação")
            );
        }

        // CENÁRIO 3: Timeout no gateway de pagamento
        if (clienteId == "CLI_TIMEOUT")
        {
            _logger.LogError(
                "Timeout ao processar pagamento. ClienteId: {ClienteId}",
                clienteId
            );
            return Resultado<DadosTransacao>.Falha(
                Erro.Tecnico("TIMEOUT_GATEWAY", "Timeout ao comunicar com gateway de pagamento")
            );
        }

        // CENÁRIO 4: Pagamento aprovado
        var transacaoId = $"TXN_{Guid.NewGuid():N}";
        var autorizacao = $"AUTH_{Random.Shared.Next(100000, 999999)}";

        // Registrar transação
        Transacoes[transacaoId] = (clienteId, valorTotal, DateTime.UtcNow, Estornado: false);

        _logger.LogInformation(
            "Pagamento aprovado. TransacaoId: {TransacaoId}, ClienteId: {ClienteId}, " +
            "Valor: {Valor:C}, Autorizacao: {Autorizacao}",
            transacaoId,
            clienteId,
            valorTotal,
            autorizacao
        );

        return Resultado<DadosTransacao>.Sucesso(
            new DadosTransacao(
                TransacaoId: transacaoId,
                Autorizacao: autorizacao,
                ValorProcessado: valorTotal
            )
        );
    }

    public async Task<Resultado<Unit>> EstornarAsync(string transacaoId)
    {
        _logger.LogWarning(
            "COMPENSAÇÃO: Estornando pagamento. TransacaoId: {TransacaoId}",
            transacaoId
        );

        // Simulação de delay de processamento
        await Task.Delay(Random.Shared.Next(100, 400));

        // Verificar se transação existe
        if (!Transacoes.ContainsKey(transacaoId))
        {
            _logger.LogError(
                "COMPENSAÇÃO: Transação não encontrada. TransacaoId: {TransacaoId}",
                transacaoId
            );
            return Resultado.Falha(
                Erro.NaoEncontrado($"Transação {transacaoId} não encontrada")
            );
        }

        var transacao = Transacoes[transacaoId];

        // Verificar se já foi estornada
        if (transacao.Estornado)
        {
            _logger.LogWarning(
                "COMPENSAÇÃO: Transação já foi estornada anteriormente (idempotência). TransacaoId: {TransacaoId}",
                transacaoId
            );
            // Retornar sucesso para garantir idempotência
            return Resultado.Sucesso();
        }

        // Marcar como estornada
        Transacoes[transacaoId] = (transacao.ClienteId, transacao.Valor, transacao.Data, Estornado: true);

        _logger.LogInformation(
            "COMPENSAÇÃO: Pagamento estornado com sucesso. TransacaoId: {TransacaoId}, " +
            "ClienteId: {ClienteId}, Valor: {Valor:C}",
            transacaoId,
            transacao.ClienteId,
            transacao.Valor
        );

        return Resultado.Sucesso();
    }
}
