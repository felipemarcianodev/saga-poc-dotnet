using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using SagaPoc.Common.ResultPattern;
using SagaPoc.ServicoPagamento.Modelos;

namespace SagaPoc.ServicoPagamento.Servicos;

/// <summary>
/// Implementação do serviço de pagamento com políticas de resiliência avançadas usando Polly.
/// Demonstra o uso de Retry, Circuit Breaker e Timeout de forma combinada.
/// </summary>
public class ServicoPagamentoComPolly : IServicoPagamento
{
    private readonly ILogger<ServicoPagamentoComPolly> _logger;
    private readonly ResiliencePipeline<Resultado<DadosTransacao>> _politicaResiliencia;

    // Simulação de banco de dados em memória (apenas para POC)
    private static readonly Dictionary<string, (string ClienteId, decimal Valor, DateTime Data, bool Estornado)> Transacoes = new();

    public ServicoPagamentoComPolly(ILogger<ServicoPagamentoComPolly> logger)
    {
        _logger = logger;
        _politicaResiliencia = CriarPoliticaResiliencia();
    }

    /// <summary>
    /// Cria a pipeline de resiliência com Retry, Circuit Breaker e Timeout.
    /// </summary>
    private ResiliencePipeline<Resultado<DadosTransacao>> CriarPoliticaResiliencia()
    {
        // Configurar Retry Policy com Exponential Backoff
        var retryPolicy = new ResiliencePipelineBuilder<Resultado<DadosTransacao>>()
            .AddRetry(new RetryStrategyOptions<Resultado<DadosTransacao>>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential, // 1s, 2s, 4s
                UseJitter = true, // Adiciona variação aleatória para evitar thundering herd

                // Retry apenas em erros transitórios
                ShouldHandle = new PredicateBuilder<Resultado<DadosTransacao>>()
                    .HandleResult(r => r.EhFalha && r.Erro.Tipo == TipoErro.Timeout)
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>(),

                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "[Polly Retry] Tentativa {AttemptNumber} após {RetryDelay}ms - Motivo: {Exception}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.Erro.Mensagem ?? "Desconhecido"
                    );
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        // Configurar Circuit Breaker
        var circuitBreakerPolicy = new ResiliencePipelineBuilder<Resultado<DadosTransacao>>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<Resultado<DadosTransacao>>
            {
                FailureRatio = 0.5, // Abrir após 50% de falhas
                MinimumThroughput = 5, // Mínimo de 5 chamadas para avaliar
                SamplingDuration = TimeSpan.FromMinutes(1), // Janela de 1 minuto
                BreakDuration = TimeSpan.FromMinutes(2), // Permanecer aberto por 2 minutos

                ShouldHandle = new PredicateBuilder<Resultado<DadosTransacao>>()
                    .HandleResult(r => r.EhFalha)
                    .Handle<Exception>(),

                OnOpened = args =>
                {
                    _logger.LogError(
                        "[Polly Circuit Breaker] Circuito ABERTO por {BreakDuration}s - Falhas recentes detectadas",
                        args.BreakDuration.TotalSeconds
                    );
                    return ValueTask.CompletedTask;
                },

                OnClosed = args =>
                {
                    _logger.LogInformation("[Polly Circuit Breaker] Circuito FECHADO - Sistema recuperado");
                    return ValueTask.CompletedTask;
                },

                OnHalfOpened = args =>
                {
                    _logger.LogWarning("[Polly Circuit Breaker] Circuito SEMI-ABERTO - Testando recuperação");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        // Criar pipeline combinada (Retry -> Circuit Breaker)
        return new ResiliencePipelineBuilder<Resultado<DadosTransacao>>()
            .AddPipeline(retryPolicy)
            .AddPipeline(circuitBreakerPolicy)
            .Build();
    }

    public async Task<Resultado<DadosTransacao>> ProcessarAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Polly] Processando pagamento com resiliência. ClienteId: {ClienteId}, Valor: {Valor:C}",
            clienteId,
            valorTotal
        );

        // Executar com política de resiliência
        return await _politicaResiliencia.ExecuteAsync(
            async ct => await ProcessarPagamentoInternoAsync(clienteId, valorTotal, formaPagamento, ct),
            cancellationToken
        );
    }

    /// <summary>
    /// Processamento interno do pagamento (sem políticas de resiliência).
    /// </summary>
    private async Task<Resultado<DadosTransacao>> ProcessarPagamentoInternoAsync(
        string clienteId,
        decimal valorTotal,
        string formaPagamento,
        CancellationToken cancellationToken)
    {
        // Validações
        if (valorTotal <= 0)
        {
            return Resultado<DadosTransacao>.Falha(
                Erro.Validacao("VALOR_INVALIDO", "Valor do pagamento deve ser maior que zero")
            );
        }

        if (valorTotal > 10000m)
        {
            return Resultado<DadosTransacao>.Falha(
                Erro.Negocio("VALOR_EXCEDE_LIMITE", "Valor excede o limite permitido (R$ 10.000,00)")
            );
        }

        var formasValidas = new[] { "CREDITO", "DEBITO", "PIX", "DINHEIRO" };
        if (!formasValidas.Contains(formaPagamento?.ToUpper() ?? ""))
        {
            return Resultado<DadosTransacao>.Falha(
                Erro.Validacao("FORMA_PAGAMENTO_INVALIDA", $"Forma de pagamento inválida")
            );
        }

        // Simulação de delay de processamento
        await Task.Delay(Random.Shared.Next(200, 800), cancellationToken);

        // Cenários de teste
        if (clienteId == "CLI_CARTAO_RECUSADO")
        {
            return Resultado<DadosTransacao>.Falha(
                Erro.Negocio("CARTAO_RECUSADO", "Cartão de crédito recusado pela operadora")
            );
        }

        if (clienteId == "CLI_TIMEOUT")
        {
            return Resultado<DadosTransacao>.Falha(
                Erro.Timeout("Timeout ao comunicar com gateway de pagamento", "TIMEOUT_GATEWAY")
            );
        }

        // Pagamento aprovado
        var transacaoId = $"TXN_{Guid.NewGuid():N}";
        var autorizacao = $"AUTH_{Random.Shared.Next(100000, 999999)}";

        Transacoes[transacaoId] = (clienteId, valorTotal, DateTime.UtcNow, Estornado: false);

        _logger.LogInformation(
            "[Polly] Pagamento aprovado. TransacaoId: {TransacaoId}, Autorizacao: {Autorizacao}",
            transacaoId,
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
        _logger.LogWarning("[Polly] COMPENSAÇÃO: Estornando pagamento. TransacaoId: {TransacaoId}", transacaoId);

        await Task.Delay(Random.Shared.Next(100, 400));

        if (!Transacoes.ContainsKey(transacaoId))
        {
            return Resultado.Falha(Erro.NaoEncontrado($"Transação {transacaoId} não encontrada"));
        }

        var transacao = Transacoes[transacaoId];

        if (transacao.Estornado)
        {
            _logger.LogWarning(
                "[Polly] COMPENSAÇÃO: Transação já estornada (idempotência). TransacaoId: {TransacaoId}",
                transacaoId
            );
            return Resultado.Sucesso();
        }

        Transacoes[transacaoId] = (transacao.ClienteId, transacao.Valor, transacao.Data, Estornado: true);

        _logger.LogInformation(
            "[Polly] COMPENSAÇÃO: Pagamento estornado com sucesso. TransacaoId: {TransacaoId}",
            transacaoId
        );

        return Resultado.Sucesso();
    }
}
