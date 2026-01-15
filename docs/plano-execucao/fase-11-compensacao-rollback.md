# FASE 11: Compensação e Rollback Completo


#### 3.11.1 Objetivos
- Implementar compensação completa em cascata
- Adicionar idempotência nas operações de compensação
- Criar estado de rastreamento de compensações
- Implementar rollback transacional
- Garantir que compensações sejam executadas em ordem reversa

#### 3.11.2 Entregas

##### 1. **Estado Estendido da SAGA com Controle de Compensação**

```csharp
public class EstadoPedido : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string EstadoAtual { get; set; } = string.Empty;

    // ... (propriedades existentes)

    // ==================== Controle de Compensação ====================

    /// <summary>
    /// Indica se o pedido está em processo de compensação.
    /// </summary>
    public bool EmCompensacao { get; set; }

    /// <summary>
    /// Timestamp de início da compensação.
    /// </summary>
    public DateTime? DataInicioCompensacao { get; set; }

    /// <summary>
    /// Timestamp de conclusão da compensação.
    /// </summary>
    public DateTime? DataConclusaoCompensacao { get; set; }

    /// <summary>
    /// Lista de passos compensados com sucesso (para rastreamento).
    /// </summary>
    public List<string> PassosCompensados { get; set; } = new();

    /// <summary>
    /// Indica se a validação do restaurante foi executada (precisa compensar).
    /// </summary>
    public bool RestauranteValidado { get; set; }

    /// <summary>
    /// Indica se o pagamento foi processado (precisa estornar).
    /// </summary>
    public bool PagamentoProcessado { get; set; }

    /// <summary>
    /// Indica se o entregador foi alocado (precisa liberar).
    /// </summary>
    public bool EntregadorAlocado { get; set; }

    /// <summary>
    /// Contador de tentativas de compensação (para idempotência).
    /// </summary>
    public int TentativasCompensacao { get; set; }

    /// <summary>
    /// Erros ocorridos durante a compensação.
    /// </summary>
    public List<string> ErrosCompensacao { get; set; } = new();
}
```

##### 2. **SAGA Estendida com Compensação Completa**

```csharp
public class PedidoSaga : Saga<PedidoSagaData>
{
    // ... (estados e eventos existentes)

    // Novos eventos de compensação
    public Event<PedidoRestauranteCancelado> PedidoCancelado { get; private set; } = null!;
    public Event<PagamentoEstornado> PagamentoEstornado { get; private set; } = null!;
    public Event<EntregadorLiberado> EntregadorLiberado { get; private set; } = null!;

    public PedidoSaga()
    {
        

        // ... (fluxo normal existente)

        // ==================== COMPENSAÇÃO: FALHA NO PAGAMENTO ====================

        During(ProcessandoPagamento,
            When(PagamentoProcessado)
                .IfElse(
                    context => context.Message.Sucesso,
                    // Sucesso
                    aprovado => aprovado
                        .Then(context =>
                        {
                            context.Saga.TransacaoId = context.Message.TransacaoId;
                            context.Saga.PagamentoProcessado = true; // Marcar para compensação
                        })
                        .TransitionTo(AlocandoEntregador)
                        .Publish(context => new AlocarEntregador(
                            context.Saga.CorrelationId,
                            context.Saga.RestauranteId,
                            context.Saga.EnderecoEntrega,
                            context.Saga.TaxaEntrega
                        )),
                    // Falha - Compensar Restaurante
                    recusado => recusado
                        .Then(context =>
                        {
                            context.Saga.MensagemErro = context.Message.MotivoFalha;
                            context.Saga.EmCompensacao = true;
                            context.Saga.DataInicioCompensacao = DateTime.UtcNow;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - COMPENSAÇÃO INICIADA");
                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Motivo: {context.Message.MotivoFalha}");
                        })
                        .TransitionTo(ExecutandoCompensacao)
                        .If(context => context.Saga.RestauranteValidado, // Só compensa se validou
                            compensa => compensa
                                .Publish(context => new CancelarPedidoRestaurante(
                                    context.Saga.CorrelationId,
                                    context.Saga.RestauranteId,
                                    context.Saga.CorrelationId
                                ))
                        )
                )
        );

        // ==================== COMPENSAÇÃO: FALHA NO ENTREGADOR ====================

        During(AlocandoEntregador,
            When(EntregadorAlocado)
                .IfElse(
                    context => context.Message.Alocado,
                    // Sucesso
                    alocado => alocado
                        .Then(context =>
                        {
                            context.Saga.EntregadorId = context.Message.EntregadorId;
                            context.Saga.TempoEntregaMinutos = context.Message.TempoEstimadoMinutos;
                            context.Saga.EntregadorAlocado = true;
                        })
                        .TransitionTo(NotificandoCliente)
                        .Publish(context => new NotificarCliente(
                            context.Saga.CorrelationId,
                            context.Saga.ClienteId,
                            $"Pedido confirmado! Entregador {context.Message.EntregadorId} a caminho.",
                            TipoNotificacao.PedidoConfirmado
                        )),
                    // Falha - Compensar TUDO (Pagamento + Restaurante)
                    semEntregador => semEntregador
                        .Then(context =>
                        {
                            context.Saga.MensagemErro = context.Message.MotivoFalha;
                            context.Saga.EmCompensacao = true;
                            context.Saga.DataInicioCompensacao = DateTime.UtcNow;

                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - COMPENSAÇÃO TOTAL INICIADA");
                            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Compensando: Pagamento + Restaurante");
                        })
                        .TransitionTo(ExecutandoCompensacao)
                        // Compensação em ORDEM REVERSA
                        // 1. Estornar pagamento
                        .If(context => context.Saga.PagamentoProcessado,
                            estorna => estorna
                                .Publish(context => new EstornarPagamento(
                                    context.Saga.CorrelationId,
                                    context.Saga.TransacaoId!
                                ))
                        )
                        // 2. Cancelar no restaurante
                        .If(context => context.Saga.RestauranteValidado,
                            cancela => cancela
                                .Publish(context => new CancelarPedidoRestaurante(
                                    context.Saga.CorrelationId,
                                    context.Saga.RestauranteId,
                                    context.Saga.CorrelationId
                                ))
                        )
                )
        );

        // ==================== TRATAMENTO DE EVENTOS DE COMPENSAÇÃO ====================

        During(ExecutandoCompensacao,
            When(PagamentoEstornado)
                .Then(context =>
                {
                    context.Saga.PassosCompensados.Add($"PagamentoEstornado:{DateTime.UtcNow}");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Pagamento estornado com sucesso");
                })
                .ThenAsync(async context =>
                {
                    // Verificar se todas as compensações foram executadas
                    await FinalizarCompensacaoSeCompleta(context);
                }),

            When(PedidoCancelado)
                .Then(context =>
                {
                    context.Saga.PassosCompensados.Add($"RestauranteCancelado:{DateTime.UtcNow}");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Pedido cancelado no restaurante");
                })
                .ThenAsync(async context =>
                {
                    await FinalizarCompensacaoSeCompleta(context);
                }),

            When(EntregadorLiberado)
                .Then(context =>
                {
                    context.Saga.PassosCompensados.Add($"EntregadorLiberado:{DateTime.UtcNow}");
                    Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Entregador liberado");
                })
                .ThenAsync(async context =>
                {
                    await FinalizarCompensacaoSeCompleta(context);
                })
        );
    }

    private async Task FinalizarCompensacaoSeCompleta<T>(BehaviorContext<EstadoPedido, T> context)
        where T : class
    {
        // Verificar se todas as compensações necessárias foram executadas
        var todasCompensadas = true;

        if (context.Saga.PagamentoProcessado &&
            !context.Saga.PassosCompensados.Any(p => p.StartsWith("PagamentoEstornado")))
        {
            todasCompensadas = false;
        }

        if (context.Saga.RestauranteValidado &&
            !context.Saga.PassosCompensados.Any(p => p.StartsWith("RestauranteCancelado")))
        {
            todasCompensadas = false;
        }

        if (context.Saga.EntregadorAlocado &&
            !context.Saga.PassosCompensados.Any(p => p.StartsWith("EntregadorLiberado")))
        {
            todasCompensadas = false;
        }

        if (todasCompensadas)
        {
            context.Saga.DataConclusaoCompensacao = DateTime.UtcNow;
            context.Saga.DataConclusao = DateTime.UtcNow;

            var duracao = (context.Saga.DataConclusaoCompensacao.Value -
                          context.Saga.DataInicioCompensacao!.Value).TotalSeconds;

            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - COMPENSAÇÃO CONCLUÍDA ({duracao:F2}s)");
            Console.WriteLine($"[SAGA] Pedido {context.Saga.CorrelationId} - Passos compensados: {context.Saga.PassosCompensados.Count}");

            // Notificar cliente
            await context.Publish(new NotificarCliente(
                context.Saga.CorrelationId,
                context.Saga.ClienteId,
                $"Pedido cancelado: {context.Saga.MensagemErro}. Todos os valores foram estornados.",
                TipoNotificacao.PedidoCancelado
            ));

            // Finalizar SAGA
            context.SetCompleted();
        }
    }
}
```

##### 3. **Consumers de Compensação com Idempotência**

```csharp
public class EstornarPagamentoConsumer : IConsumer<EstornarPagamento>
{
    private readonly IServicoPagamento _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<EstornarPagamentoConsumer> _logger;

    public async Task Consume(ConsumeContext<EstornarPagamento> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;
        var transacaoId = context.Message.TransacaoId;
        var chaveIdempotencia = $"estorno:{transacaoId}";

        _logger.LogInformation(
            "[Pagamento] Iniciando estorno - CorrelacaoId: {CorrelacaoId}, TransacaoId: {TransacaoId}",
            correlacaoId,
            transacaoId
        );

        // ==================== IDEMPOTÊNCIA ====================
        // Verificar se já foi estornado
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[Pagamento] Estorno já processado anteriormente - TransacaoId: {TransacaoId}",
                transacaoId
            );

            // Responder com sucesso mesmo assim (idempotência)
            await context.Publish(new PagamentoEstornado(
                correlacaoId,
                Sucesso: true,
                TransacaoId: transacaoId
            ));
            return;
        }

        // ==================== PROCESSAR ESTORNO ====================
        var resultado = await _servico.EstornarAsync(transacaoId, context.CancellationToken);

        resultado.Match(
            sucesso: _ =>
            {
                _logger.LogInformation(
                    "[Pagamento] Estorno realizado com sucesso - TransacaoId: {TransacaoId}",
                    transacaoId
                );
            },
            falha: erro =>
            {
                _logger.LogError(
                    "[Pagamento] Falha ao estornar - TransacaoId: {TransacaoId}, Erro: {Erro}",
                    transacaoId,
                    erro.Mensagem
                );
            }
        );

        // Marcar como processado
        if (resultado.EhSucesso)
        {
            await _idempotencia.MarcarProcessadoAsync(
                chaveIdempotencia,
                new { transacaoId, data = DateTime.UtcNow }
            );
        }

        await context.Publish(new PagamentoEstornado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            TransacaoId: transacaoId
        ));
    }
}

public class CancelarPedidoRestauranteConsumer : IConsumer<CancelarPedidoRestaurante>
{
    private readonly IServicoRestaurante _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<CancelarPedidoRestauranteConsumer> _logger;

    public async Task Consume(ConsumeContext<CancelarPedidoRestaurante> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;
        var pedidoId = context.Message.PedidoId;
        var chaveIdempotencia = $"cancelamento:{pedidoId}";

        _logger.LogInformation(
            "[Restaurante] Cancelando pedido - CorrelacaoId: {CorrelacaoId}, PedidoId: {PedidoId}",
            correlacaoId,
            pedidoId
        );

        // Idempotência
        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[Restaurante] Cancelamento já processado - PedidoId: {PedidoId}",
                pedidoId
            );

            await context.Publish(new PedidoRestauranteCancelado(
                correlacaoId,
                Sucesso: true,
                PedidoId: pedidoId
            ));
            return;
        }

        var resultado = await _servico.CancelarPedidoAsync(
            context.Message.RestauranteId,
            pedidoId,
            context.CancellationToken
        );

        if (resultado.EhSucesso)
        {
            await _idempotencia.MarcarProcessadoAsync(chaveIdempotencia, new { pedidoId });
        }

        await context.Publish(new PedidoRestauranteCancelado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            PedidoId: pedidoId
        ));
    }
}

public class LiberarEntregadorConsumer : IConsumer<LiberarEntregador>
{
    private readonly IServicoEntregador _servico;
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly ILogger<LiberarEntregadorConsumer> _logger;

    public async Task Consume(ConsumeContext<LiberarEntregador> context)
    {
        var correlacaoId = context.Message.CorrelacaoId;
        var entregadorId = context.Message.EntregadorId;
        var chaveIdempotencia = $"liberacao:{entregadorId}:{correlacaoId}";

        _logger.LogInformation(
            "[Entregador] Liberando entregador - CorrelacaoId: {CorrelacaoId}, EntregadorId: {EntregadorId}",
            correlacaoId,
            entregadorId
        );

        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning(
                "[Entregador] Liberação já processada - EntregadorId: {EntregadorId}",
                entregadorId
            );

            await context.Publish(new EntregadorLiberado(
                correlacaoId,
                Sucesso: true,
                EntregadorId: entregadorId
            ));
            return;
        }

        var resultado = await _servico.LiberarAsync(entregadorId, context.CancellationToken);

        if (resultado.EhSucesso)
        {
            await _idempotencia.MarcarProcessadoAsync(chaveIdempotencia, new { entregadorId });
        }

        await context.Publish(new EntregadorLiberado(
            correlacaoId,
            Sucesso: resultado.EhSucesso,
            EntregadorId: entregadorId
        ));
    }
}
```

##### 4. **Repositório de Idempotência (InMemory para POC)**

```csharp
public interface IRepositorioIdempotencia
{
    Task<bool> JaProcessadoAsync(string chave);
    Task MarcarProcessadoAsync(string chave, object dados);
}

public class RepositorioIdempotenciaInMemory : IRepositorioIdempotencia
{
    private readonly ConcurrentDictionary<string, (DateTime DataProcessamento, object Dados)> _cache = new();
    private readonly TimeSpan _tempoExpiracao = TimeSpan.FromHours(24);

    public Task<bool> JaProcessadoAsync(string chave)
    {
        if (_cache.TryGetValue(chave, out var entrada))
        {
            // Verificar se não expirou
            if (DateTime.UtcNow - entrada.DataProcessamento < _tempoExpiracao)
            {
                return Task.FromResult(true);
            }

            // Remover entrada expirada
            _cache.TryRemove(chave, out _);
        }

        return Task.FromResult(false);
    }

    public Task MarcarProcessadoAsync(string chave, object dados)
    {
        _cache[chave] = (DateTime.UtcNow, dados);
        return Task.CompletedTask;
    }
}

// Registrar no DI
builder.Services.AddSingleton<IRepositorioIdempotencia, RepositorioIdempotenciaInMemory>();
```

##### 5. **Novos Contratos de Mensagens de Compensação**

```csharp
// Respostas de compensação
public record PagamentoEstornado(
    Guid CorrelacaoId,
    bool Sucesso,
    string TransacaoId
);

public record PedidoRestauranteCancelado(
    Guid CorrelacaoId,
    bool Sucesso,
    Guid PedidoId
);

public record EntregadorLiberado(
    Guid CorrelacaoId,
    bool Sucesso,
    string EntregadorId
);
```

#### 3.11.3 Critérios de Aceitação
- [ ] Compensações executam em ordem reversa corretamente
- [ ] Idempotência garante que compensações não sejam duplicadas
- [ ] Estado da SAGA rastreia passos compensados
- [ ] Logs mostram claramente o fluxo de compensação
- [ ] Compensação parcial funciona (apenas o que foi executado)
- [ ] Compensação total funciona (todos os passos)

---

