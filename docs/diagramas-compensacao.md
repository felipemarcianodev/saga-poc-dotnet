# Diagramas de Compensação - SAGA Pattern

Este documento apresenta os diagramas detalhados dos fluxos de compensação implementados no projeto SAGA POC.

---

## 1. Fluxo de Compensação - Falha no Pagamento

Cenário onde a validação do restaurante é bem-sucedida, mas o processamento do pagamento falha.

```mermaid
sequenceDiagram
    participant API
    participant SAGA
    participant Restaurante
    participant Pagamento

    API->>SAGA: IniciarPedido
    SAGA->>Restaurante: ValidarPedido
    Restaurante-->>SAGA: Validado ✅

    SAGA->>Pagamento: ProcessarPagamento
    Pagamento-->>SAGA: Falha ❌

    Note over SAGA: Iniciando Compensação

    SAGA->>Restaurante: CancelarPedido
    Restaurante-->>SAGA: Cancelado ✅

    Note over SAGA: Compensação Concluída
    SAGA-->>API: Pedido Compensado
```

### Detalhes do Fluxo

1. **Início**: API recebe requisição de criação de pedido
2. **Validação**: Restaurante valida disponibilidade dos itens
3. **Falha no Pagamento**: Cartão recusado, saldo insuficiente, etc.
4. **Compensação**: Cancela a reserva dos itens no restaurante
5. **Resultado**: SAGA finalizada com status "Compensado"

### Estados da SAGA

- `Iniciado` → `AguardandoValidacaoRestaurante` → `AguardandoPagamento` → `ExecutandoCompensacao` → `Compensado`

---

## 2. Fluxo de Compensação - Falha no Entregador (Total)

Cenário onde restaurante e pagamento são bem-sucedidos, mas a alocação do entregador falha, exigindo compensação total.

```mermaid
sequenceDiagram
    participant SAGA
    participant Restaurante
    participant Pagamento
    participant Entregador

    SAGA->>Restaurante: ValidarPedido
    Restaurante-->>SAGA: Validado ✅

    SAGA->>Pagamento: ProcessarPagamento
    Pagamento-->>SAGA: Aprovado ✅

    SAGA->>Entregador: AlocarEntregador
    Entregador-->>SAGA: Indisponível ❌

    Note over SAGA: Compensação Total

    par Compensar em Paralelo
        SAGA->>Pagamento: EstornarPagamento
        SAGA->>Restaurante: CancelarPedido
    end

    Pagamento-->>SAGA: Estornado ✅
    Restaurante-->>SAGA: Cancelado ✅

    Note over SAGA: Compensação Concluída
```

### Detalhes do Fluxo

1. **Validação**: Restaurante confirma disponibilidade
2. **Pagamento**: Cobrança aprovada e confirmada
3. **Falha na Entrega**: Nenhum entregador disponível na região
4. **Compensação Paralela**:
   - Estorno do pagamento
   - Cancelamento do pedido no restaurante
5. **Resultado**: SAGA finalizada com status "Compensado"

### Estados da SAGA

- `Iniciado` → `AguardandoValidacaoRestaurante` → `AguardandoPagamento` → `AguardandoEntregador` → `ExecutandoCompensacao` → `Compensado`

---

## 3. Fluxo Completo de Sucesso (Referência)

Para comparação, este é o fluxo quando tudo funciona corretamente.

```mermaid
sequenceDiagram
    participant API
    participant SAGA
    participant Restaurante
    participant Pagamento
    participant Entregador

    API->>SAGA: IniciarPedido

    SAGA->>Restaurante: ValidarPedido
    Restaurante-->>SAGA: Validado ✅

    SAGA->>Pagamento: ProcessarPagamento
    Pagamento-->>SAGA: Aprovado ✅

    SAGA->>Entregador: AlocarEntregador
    Entregador-->>SAGA: Alocado ✅

    Note over SAGA: Pedido Concluído
    SAGA-->>API: Pedido Confirmado
```

### Estados da SAGA

- `Iniciado` → `AguardandoValidacaoRestaurante` → `AguardandoPagamento` → `AguardandoEntregador` → `Concluido`

---

## 4. Diagrama de Estados da SAGA

```mermaid
stateDiagram-v2
    [*] --> Iniciado

    Iniciado --> AguardandoValidacaoRestaurante: IniciarPedido

    AguardandoValidacaoRestaurante --> AguardandoPagamento: PedidoValidado
    AguardandoValidacaoRestaurante --> ExecutandoCompensacao: ValidacaoFalhou

    AguardandoPagamento --> AguardandoEntregador: PagamentoAprovado
    AguardandoPagamento --> ExecutandoCompensacao: PagamentoFalhou

    AguardandoEntregador --> Concluido: EntregadorAlocado
    AguardandoEntregador --> ExecutandoCompensacao: EntregadorIndisponivel

    ExecutandoCompensacao --> Compensado: CompensacaoConcluida

    Concluido --> [*]
    Compensado --> [*]
```

---

## 5. Fluxo de Compensação com Retry

Cenário onde um passo de compensação falha e precisa ser reexecutado.

```mermaid
sequenceDiagram
    participant SAGA
    participant Pagamento
    participant Restaurante

    Note over SAGA: Compensação Iniciada

    SAGA->>Pagamento: EstornarPagamento
    Pagamento-->>SAGA: Timeout ⏱️

    Note over SAGA: Retry 1/3
    SAGA->>Pagamento: EstornarPagamento
    Pagamento-->>SAGA: Timeout ⏱️

    Note over SAGA: Retry 2/3
    SAGA->>Pagamento: EstornarPagamento
    Pagamento-->>SAGA: Estornado ✅

    SAGA->>Restaurante: CancelarPedido
    Restaurante-->>SAGA: Cancelado ✅

    Note over SAGA: Compensação Concluída
```

### Política de Retry

- **Tentativas**: 3 vezes
- **Intervalo**: Exponencial (1s, 2s, 4s)
- **Ação se falhar**: Enviar para Dead Letter Queue

---

## 6. Compensação com Idempotência

Diagrama mostrando como a idempotência previne execução duplicada.

```mermaid
sequenceDiagram
    participant SAGA
    participant Pagamento
    participant Cache

    SAGA->>Pagamento: EstornarPagamento (Tentativa 1)
    Pagamento->>Cache: Verificar idempotência (chave: transacao-123)
    Cache-->>Pagamento: Não encontrado
    Pagamento->>Cache: Armazenar (transacao-123: "processando")
    Pagamento-->>SAGA: Estornado ✅

    Note over SAGA: Retry acidental ou mensagem duplicada

    SAGA->>Pagamento: EstornarPagamento (Tentativa 2)
    Pagamento->>Cache: Verificar idempotência (chave: transacao-123)
    Cache-->>Pagamento: Já processado ✅
    Pagamento-->>SAGA: OK (Idempotente)
```

---

## 7. Matriz de Compensação

Tabela de referência rápida para entender quais compensações são executadas em cada cenário.

| Falha no Passo | Restaurante | Pagamento | Entregador | Compensações Executadas |
|---------------|-------------|-----------|------------|------------------------|
| Validação Restaurante | ❌ | - | - | Nenhuma |
| Pagamento | ✅ | ❌ | - | Cancelar Restaurante |
| Entregador | ✅ | ✅ | ❌ | Estornar Pagamento + Cancelar Restaurante |

---

## 8. Tempo de Execução Típico

```mermaid
gantt
    title Duração Típica da SAGA (Sucesso)
    dateFormat  ss
    axisFormat  %Ss

    section Validação
    Validar Restaurante     :a1, 00, 2s

    section Pagamento
    Processar Pagamento     :a2, after a1, 3s

    section Entrega
    Alocar Entregador       :a3, after a2, 2s

    section Total
    SAGA Concluída          :milestone, after a3, 0s
```

**Tempo Total**: ~7 segundos (em condições normais)

---

## 9. Cenário de Compensação Parcial

Quando apenas uma parte da transação precisa ser compensada.

```mermaid
sequenceDiagram
    participant SAGA
    participant Restaurante
    participant Pagamento
    participant Estoque

    SAGA->>Restaurante: ValidarPedido
    Restaurante-->>SAGA: Validado ✅

    SAGA->>Estoque: ReservarItens
    Estoque-->>SAGA: Reservado ✅

    SAGA->>Pagamento: ProcessarPagamento
    Pagamento-->>SAGA: Falha (Saldo Insuficiente) ❌

    Note over SAGA: Compensação Parcial

    SAGA->>Estoque: LiberarReserva
    Estoque-->>SAGA: Liberado ✅

    SAGA->>Restaurante: CancelarPedido
    Restaurante-->>SAGA: Cancelado ✅
```

---

## 10. Observabilidade da Compensação

Eventos e logs gerados durante o processo de compensação.

```mermaid
sequenceDiagram
    participant SAGA
    participant Logger
    participant Metrics
    participant Alertas

    Note over SAGA: Compensação Iniciada

    SAGA->>Logger: Log: "Iniciando compensação para pedido {id}"
    SAGA->>Metrics: Incrementar: saga_compensacoes_total

    loop Para cada passo
        SAGA->>Logger: Log: "Compensando {passo}"
        SAGA->>Metrics: Incrementar: passo_compensado_{nome}
    end

    alt Compensação com falhas
        SAGA->>Alertas: Enviar alerta: "Compensação com retry"
    end

    Note over SAGA: Compensação Concluída

    SAGA->>Logger: Log: "Compensação finalizada - {passos} passos"
    SAGA->>Metrics: Registrar: saga_compensacao_duracao_ms
```

---

## Notas Importantes

### Princípios de Compensação

1. **Ordem Reversa**: Compensações sempre executam na ordem inversa das transações
2. **Idempotência**: Cada compensação pode ser executada múltiplas vezes com o mesmo resultado
3. **Resiliência**: Compensações têm retry automático com backoff exponencial
4. **Auditoria**: Todos os passos de compensação são logados e rastreáveis

### Garantias

- ✅ Eventual consistência através das compensações
- ✅ Nenhuma transação fica "meio processada"
- ✅ Todos os passos bem-sucedidos são compensados em caso de falha
- ✅ Sistema retorna a um estado consistente mesmo após falhas

---

**Última atualização**: 2026-01-07
**Versão**: 1.0
