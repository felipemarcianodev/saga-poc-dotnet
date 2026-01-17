using SagaPoc.Common.ResultPattern;
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

    /// <summary>
    /// Processa um pagamento usando validação em cascata com Result Pattern.
    /// </summary>
    public async Task<Resultado<DadosTransacao>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processando pagamento. ClienteId: {ClienteId}, Valor: {Valor:C}, FormaPagamento: {FormaPagamento}",
            clienteId,
            valorTotal,
            formaPagamento
        );

        // Validação em cascata (Railway-Oriented Programming)
        // 1. Validar valor
        var resultadoValor = ValidarValor(valorTotal);
        if (resultadoValor.EhFalha)
            return Resultado<DadosTransacao>.Falha(resultadoValor.Erro);

        // 2. Validar forma de pagamento (encadeamento com Bind)
        var resultadoForma = resultadoValor.Bind(_ => ValidarFormaPagamento(formaPagamento));
        if (resultadoForma.EhFalha)
            return Resultado<DadosTransacao>.Falha(resultadoForma.Erro);

        // 3. Processar pagamento no gateway (encadeamento com BindAsync)
        return await resultadoForma
            .BindAsync(_ => ProcessarPagamentoGatewayAsync(clienteId, valorTotal, formaPagamento, cancellationToken));
    }

    /// <summary>
    /// Etapa 1: Valida o valor da transação.
    /// </summary>
    private Resultado<Unit> ValidarValor(decimal valorTotal)
    {
        if (valorTotal <= 0)
        {
            return Resultado.Falha(
                Erro.Validacao("VALOR_INVALIDO", "Valor do pagamento deve ser maior que zero")
            );
        }

        if (valorTotal > 10000m)
        {
            return Resultado.Falha(
                Erro.Negocio("VALOR_EXCEDE_LIMITE", "Valor excede o limite permitido (R$ 10.000,00)")
            );
        }

        return Resultado.Sucesso();
    }

    /// <summary>
    /// Etapa 2: Valida a forma de pagamento.
    /// </summary>
    private Resultado<Unit> ValidarFormaPagamento(string formaPagamento)
    {
        var formasValidas = new[] { "CREDITO", "DEBITO", "PIX", "DINHEIRO" };

        if (string.IsNullOrWhiteSpace(formaPagamento))
        {
            return Resultado.Falha(
                Erro.Validacao("FORMA_PAGAMENTO_VAZIA", "Forma de pagamento é obrigatória")
            );
        }

        if (!formasValidas.Contains(formaPagamento.ToUpper()))
        {
            return Resultado.Falha(
                Erro.Validacao(
                    "FORMA_PAGAMENTO_INVALIDA",
                    $"Forma de pagamento '{formaPagamento}' não é válida. Opções: {string.Join(", ", formasValidas)}"
                )
            );
        }

        return Resultado.Sucesso();
    }

    /// <summary>
    /// Etapa 3: Processa o pagamento no gateway.
    /// </summary>
    private async Task<Resultado<DadosTransacao>> ProcessarPagamentoGatewayAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken = default)
    {
        // Simulação de delay de processamento (integração com gateway)
        await Task.Delay(Random.Shared.Next(200, 800), cancellationToken);

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
                Erro.Timeout("Timeout ao comunicar com gateway de pagamento", "TIMEOUT_GATEWAY")
            );
        }

        // CENÁRIO 4: Erro no gateway (sistema externo)
        if (clienteId == "CLI_ERRO_GATEWAY")
        {
            _logger.LogError(
                "Erro no gateway de pagamento. ClienteId: {ClienteId}",
                clienteId
            );
            return Resultado<DadosTransacao>.Falha(
                Erro.Externo("Gateway de pagamento retornou erro", "ERRO_GATEWAY")
            );
        }

        // CENÁRIO 5: Pagamento aprovado
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
