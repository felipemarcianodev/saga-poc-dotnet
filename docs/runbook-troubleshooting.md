# Runbook de Troubleshooting - SAGA POC

Este documento fornece procedimentos passo a passo para diagnosticar e resolver problemas comuns no sistema SAGA.

---

## Índice

1. [SAGA Travada](#1-saga-travada)
2. [Mensagens em Dead Letter Queue](#2-mensagens-em-dead-letter-queue)
3. [Compensação Falhou](#3-compensacao-falhou)
4. [Alta Latência nas SAGAs](#4-alta-latencia-nas-sagas)
5. [Circuit Breaker Aberto](#5-circuit-breaker-aberto)
6. [Perda de Mensagens](#6-perda-de-mensagens)
7. [Memória Alta no Orquestrador](#7-memoria-alta-no-orquestrador)
8. [Duplicação de Pedidos](#8-duplicacao-de-pedidos)

---

## 1. SAGA Travada

### Sintomas
- SAGA não progride há mais de 10 minutos
- Estado da SAGA permanece inalterado
- Cliente reporta que pedido está "pendente" por muito tempo
- Mensagens presas na fila sem serem processadas

### Diagnóstico

#### Passo 1: Verificar Estado da SAGA

```bash
# Via API
curl http://localhost:5000/api/pedidos/{pedidoId}/estado
```

```csharp
// Query direta no repositório (MongoDB - se usar persistência MongoDB)
db.PedidoSagaData.find({
    Id: "{correlationId}"
}).sort({ DataInicio: -1 })
```

```csharp
// Query no Postgres(se usar persistência SQL)
SELECT
    CorrelationId,
    EstadoAtual,
    DataInicio,
    UltimaAtualizacao,
    EmCompensacao,
    PassosCompensados
FROM PedidoSagaData
WHERE Id = '{id}'
ORDER BY DataInicio DESC;
```

#### Passo 2: Verificar Filas do RabbitMQ

```bash
# Acessar Management UI
http://localhost:15672

# Ou via API do RabbitMQ
curl -u guest:guest http://localhost:15672/api/queues/%2F/fila-restaurante
```

Verificar:
- `messages_ready`: Mensagens aguardando processamento
- `messages_unacknowledged`: Mensagens em processamento
- `consumers`: Número de consumidores ativos

#### Passo 3: Verificar Logs do Orquestrador

```bash
# Filtrar por CorrelationId
grep "{correlationId}" logs/saga-*.log

# Verificar últimas 100 linhas de erros
tail -n 100 logs/saga-*.log | grep "ERROR"
```

#### Passo 4: Verificar Health dos Serviços

```bash
# Restaurante
curl http://localhost:5001/health

# Pagamento
curl http://localhost:5002/health

# Entregador
curl http://localhost:5003/health

# Orquestrador
curl http://localhost:5004/health
```

### Ações Corretivas

#### Caso 1: Timeout em Serviço Downstream

**Diagnóstico**: Log mostra "Request timeout after 30 seconds"

**Ação**:
```bash
# 1. Verificar saúde do serviço
curl http://localhost:5001/health

# 2. Se serviço está down, reiniciar
docker restart saga-poc-restaurante

# 3. Aguardar 30s e verificar retry automático
```

#### Caso 2: Mensagem Presa em Processamento

**Diagnóstico**: `messages_unacknowledged > 0` mas sem progresso

**Ação**:
```bash
# 1. Identificar consumidor travado
curl -u guest:guest http://localhost:15672/api/channels

# 2. Fechar conexão do consumidor problemático
# (isso fará a mensagem retornar à fila)
curl -u guest:guest -X DELETE \
  http://localhost:15672/api/channels/{channel_name}

# 3. Reiniciar consumidor
docker restart saga-poc-orquestrador
```

#### Caso 3: Dead Letter Queue com Mensagens

**Diagnóstico**: Mensagem foi para DLQ após 3 tentativas

**Ação**: Ver seção [Mensagens em Dead Letter Queue](#2-mensagens-em-dead-letter-queue)

#### Caso 4: Estado Inconsistente

**Diagnóstico**: Estado da SAGA não corresponde às mensagens processadas

**Ação**:
```csharp
// Executar compensação manual via API
POST http://localhost:5000/api/pedidos/{pedidoId}/compensar
Content-Type: application/json

{
  "motivo": "Estado inconsistente detectado em troubleshooting",
  "executarEstorno": true,
  "notificarCliente": true
}
```

### Scripts de Utilidade

```bash
# Script para verificar SAGAs travadas
#!/bin/bash
# check-stuck-sagas.sh

THRESHOLD_MINUTES=10

# Query no MongoDB (se usar persistência MongoDB)
mongo saga-poc --eval "
  db.PedidoSagaData.find({
    EstadoAtual: { \$nin: ['Concluido', 'Compensado', 'Falhou'] },
    UltimaAtualizacao: {
      \$lt: new Date(Date.now() - $THRESHOLD_MINUTES * 60 * 1000)
    }
  }).forEach(saga => {
    print('SAGA Travada: ' + saga.Id + ' - Estado: ' + saga.EstadoAtual);
  })
"
```

---

## 2. Mensagens em Dead Letter Queue

### Sintomas
- Mensagens aparecem na Dead Letter Queue (DLQ)
- Logs mostram "Message moved to DLQ after 3 retries"
- Taxa de erro acima de 5%

### Diagnóstico

#### Passo 1: Inspecionar Mensagens na DLQ

```bash
# Via RabbitMQ Management
http://localhost:15672/#/queues/%2F/fila-pagamento-dlq

# Obter mensagem via API
curl -u guest:guest -X POST \
  http://localhost:15672/api/queues/%2F/fila-pagamento-dlq/get \
  -H "content-type: application/json" \
  -d '{"count":1,"ackmode":"ack_requeue_false","encoding":"auto"}'
```

#### Passo 2: Analisar Motivo da Falha

```bash
# Verificar headers da mensagem
# Procurar por: x-exception-message, x-exception-stacktrace
```

Motivos comuns:
- **Validation failed**: Dados inválidos na mensagem
- **Service unavailable**: Serviço downstream fora do ar
- **Timeout**: Operação demorou mais que o limite
- **Duplicate key**: Violação de idempotência

### Ações Corretivas

#### Caso 1: Validação Falhou

**Diagnóstico**: `x-exception-message: "ClienteId inválido"`

**Ação**:
```csharp
// 1. Corrigir dados no banco (se possível)
UPDATE Pedidos SET ClienteId = 'CLI001'
WHERE PedidoId = '{id}';

// 2. Reprocessar mensagem manualmente
POST http://localhost:5000/api/pedidos/{pedidoId}/reprocessar
```

#### Caso 2: Serviço Temporariamente Indisponível

**Diagnóstico**: `x-exception-message: "Connection refused"`

**Ação**:
```bash
# 1. Verificar se serviço voltou
curl http://localhost:5002/health

# 2. Mover mensagens de volta à fila principal
curl -u guest:guest -X POST \
  http://localhost:15672/api/shovel/move-from-dlq
```

#### Caso 3: Erro de Lógica no Código

**Diagnóstico**: `x-exception-message: "NullReferenceException"`

**Ação**:
1. Corrigir código e fazer deploy
2. Aguardar retry automático ou mover manualmente de DLQ

---

## 3. Compensação Falhou

### Sintomas
- SAGA em estado "ExecutandoCompensacao" por muito tempo
- Logs mostram "Compensação falhou após 3 tentativas"
- Cliente cobrado mas pedido cancelado (inconsistência)

### Diagnóstico

#### Passo 1: Verificar Passos Compensados

```bash
# Via API
curl http://localhost:5000/api/pedidos/{pedidoId}/compensacao/status
```

```json
{
  "emCompensacao": true,
  "passosCompensados": ["RestauranteCancelado"],
  "passosPendentes": ["PagamentoEstornado"],
  "tentativasCompensacao": 2,
  "ultimoErro": "Timeout ao estornar pagamento"
}
```

#### Passo 2: Verificar Sistema de Pagamento

```bash
# Verificar se pagamento foi realmente processado
curl http://localhost:5002/api/pagamentos/{transacaoId}/status

# Verificar se estorno já foi executado
curl http://localhost:5002/api/pagamentos/{transacaoId}/estornos
```

### Ações Corretivas

#### Caso 1: Estorno Já Foi Executado (Idempotência)

**Diagnóstico**: Sistema de pagamento retorna "Estorno já processado"

**Ação**:
```csharp
// Marcar compensação como concluída manualmente
POST http://localhost:5000/api/pedidos/{pedidoId}/compensacao/marcar-completa

{
  "passo": "PagamentoEstornado",
  "motivo": "Verificado manualmente - estorno já processado"
}
```

#### Caso 2: Sistema de Pagamento Indisponível

**Diagnóstico**: Timeout contínuo ao chamar serviço de estorno

**Ação**:
```bash
# 1. Verificar circuit breaker
curl http://localhost:5004/api/circuit-breaker/status

# 2. Forçar reset do circuit breaker (se necessário)
POST http://localhost:5004/api/circuit-breaker/reset

# 3. Aguardar retry automático ou executar manualmente
POST http://localhost:5000/api/pedidos/{pedidoId}/compensacao/retry
```

#### Caso 3: Compensação Manual Necessária

**Diagnóstico**: Sistema não consegue estornar automaticamente

**Ação**:
1. Executar estorno manual no sistema de pagamento
2. Registrar compensação manual na SAGA:

```csharp
POST http://localhost:5000/api/pedidos/{pedidoId}/compensacao/manual

{
  "passo": "PagamentoEstornado",
  "transacaoId": "TXN123",
  "executadoPor": "Operador João Silva",
  "evidencia": "Ticket ServiceNow #12345",
  "dataHoraExecucao": "2026-01-07T14:30:00Z"
}
```

---

## 4. Alta Latência nas SAGAs

### Sintomas
- SAGAs demorando mais de 30 segundos para concluir
- Clientes reclamando de lentidão
- Métricas mostram `saga_duracao_segundos` acima de P95 = 20s

### Diagnóstico

#### Passo 1: Identificar Gargalo

```bash
# Verificar métricas de duração por passo
curl http://localhost:5004/metrics | grep saga_passo_duracao
```

```bash
# Analisar logs para identificar passo lento
grep "Passo.*concluído" logs/saga-*.log | \
  awk '{print $5, $8}' | \
  sort -k2 -rn | \
  head -10
```

#### Passo 2: Verificar Recursos

```bash
# CPU e memória dos containers
docker stats

# Conexões de banco de dados
# MongoDB
mongo saga-poc --eval "db.serverStatus().connections"

# Verificar latência de rede entre serviços
time curl http://localhost:5002/health
```

### Ações Corretivas

#### Caso 1: Serviço Específico Lento

**Diagnóstico**: Serviço de Pagamento está demorando 15s

**Ação**:
```bash
# 1. Investigar logs do serviço
docker logs saga-poc-pagamento --tail 100

# 2. Verificar health check detalhado
curl http://localhost:5002/health/detailed

# 3. Escalar serviço (se necessário)
docker-compose up -d --scale pagamento=3
```

#### Caso 2: Banco de Dados Lento

**Diagnóstico**: Queries no MongoDB demorando muito

**Ação**:
```javascript
// 1. Analisar slow queries
db.setProfilingLevel(1, { slowms: 100 })
db.system.profile.find().sort({ ts: -1 }).limit(10)

// 2. Criar índices faltantes
db.EstadoPedido.createIndex({ CorrelationId: 1 })
db.EstadoPedido.createIndex({ EstadoAtual: 1, UltimaAtualizacao: -1 })

// 3. Verificar estatísticas de índices
db.EstadoPedido.stats()
```

#### Caso 3: Muitas SAGAs Concorrentes

**Diagnóstico**: Sistema está processando 500+ SAGAs simultaneamente

**Ação**:
```csharp
// Configurar limit de concorrência no Rebus
builder.Services.AddRebus((configure, provider) => configure
    .Transport(t => t.UseRabbitMq(...))
    .Options(o =>
    {
        o.SetNumberOfWorkers(1);  // Manter 1 worker
        o.SetMaxParallelism(50);  // Reduzir de 100 para 50
    })
);
```

---

## 5. Circuit Breaker Aberto

### Sintomas
- Logs mostram "Circuit breaker is open"
- Requisições falhando imediatamente sem tentar chamar serviço
- Taxa de erro 100% para um serviço específico

### Diagnóstico

#### Passo 1: Verificar Estado do Circuit Breaker

```bash
# Via endpoint de diagnóstico
curl http://localhost:5004/api/circuit-breaker/status
```

```json
{
  "pagamento": {
    "estado": "Open",
    "falhasConsecutivas": 15,
    "ultimaFalha": "2026-01-07T14:25:30Z",
    "proximaTentativa": "2026-01-07T14:30:30Z",
    "motivoAbertura": "Timeout threshold exceeded"
  }
}
```

#### Passo 2: Verificar Saúde do Serviço Downstream

```bash
# Testar manualmente
curl -v http://localhost:5002/health
```

### Ações Corretivas

#### Caso 1: Serviço Voltou ao Normal

**Diagnóstico**: Health check retorna 200 OK

**Ação**:
```bash
# 1. Aguardar reset automático (5 minutos)
# OU
# 2. Forçar reset manual
POST http://localhost:5004/api/circuit-breaker/reset/pagamento

# 3. Verificar se voltou a funcionar
curl http://localhost:5004/api/circuit-breaker/status
```

#### Caso 2: Serviço Ainda Está Com Problemas

**Diagnóstico**: Health check retorna 503 ou timeout

**Ação**:
```bash
# 1. Investigar logs do serviço
docker logs saga-poc-pagamento --tail 200

# 2. Reiniciar serviço
docker restart saga-poc-pagamento

# 3. Monitorar recuperação
watch -n 5 'curl -s http://localhost:5002/health | jq'
```

---

## 6. Perda de Mensagens

### Sintomas
- Pedido criado mas SAGA nunca iniciou
- Mensagem enviada mas nunca processada
- Diferença entre contador de mensagens enviadas e recebidas

### Diagnóstico

#### Passo 1: Verificar Filas

```bash
# Verificar todas as filas
curl -u guest:guest http://localhost:15672/api/queues | jq '.[] | {name, messages}'
```

#### Passo 2: Verificar Durabilidade

```bash
# Verificar se filas são duráveis
curl -u guest:guest http://localhost:15672/api/queues/%2F/fila-pagamento | \
  jq '.durable'
```

#### Passo 3: Verificar Logs de Publicação

```bash
grep "Publicando mensagem" logs/saga-*.log | \
  grep "{correlationId}"
```

### Ações Corretivas

#### Caso 1: Fila Não Durável

**Diagnóstico**: `durable: false`

**Ação**:
```csharp
// Rebus cria filas duráveis por padrão
// Se necessário, verificar configuração do RabbitMQ
// Filas criadas pelo Rebus são automaticamente:
// - Durable: true
// - AutoDelete: false
// - Exclusive: false

// Recriar fila manualmente via RabbitMQ Management se necessário
```

#### Caso 2: RabbitMQ Reiniciou

**Diagnóstico**: Logs mostram "Connection lost" no horário do problema

**Ação**:
```bash
# 1. Verificar se mensagens foram persistidas
curl -u guest:guest http://localhost:15672/api/queues | \
  jq '.[] | select(.messages > 0)'

# 2. Se perdidas, reprocessar a partir de evento source
POST http://localhost:5000/api/pedidos/reprocessar-perdidos

{
  "dataInicio": "2026-01-07T14:00:00Z",
  "dataFim": "2026-01-07T15:00:00Z"
}
```

---

## 7. Memória Alta no Orquestrador

### Sintomas
- Container usando > 80% da memória alocada
- Logs de "OutOfMemoryException"
- Performance degradada gradualmente

### Diagnóstico

#### Passo 1: Verificar Uso de Memória

```bash
# Por container
docker stats saga-poc-orquestrador

# Dentro do container (.NET)
curl http://localhost:5004/api/diagnostics/memory
```

#### Passo 2: Capturar Heap Dump

```bash
# Instalar dotnet-dump no container
docker exec -it saga-poc-orquestrador bash
dotnet tool install -g dotnet-dump

# Capturar dump
dotnet-dump collect -p 1 -o /dumps/heap.dmp

# Analisar dump
dotnet-dump analyze /dumps/heap.dmp
> dumpheap -stat
```

### Ações Corretivas

#### Caso 1: Memory Leak em SAGA State

**Diagnóstico**: Muitas instâncias de `PedidoSagaData` na memória

**Ação**:
```csharp
// Para POC com InMemory, não há persistência
// Para produção com MongoDB:
builder.Services.AddRebus((configure, provider) => configure
    .Transport(t => t.UseRabbitMq(...))
    .Sagas(s => s.UseMongoDb(connectionString, "sagas"))
);

// Criar índice TTL no MongoDB para cleanup automático
db.PedidoSagaData.createIndex(
    { "DataConclusao": 1 },
    { expireAfterSeconds: 604800 } // 7 dias
)

// Ou usar Postgres:
// .Sagas(s => s.UseNpgsql(connectionString, "Sagas"))
```

#### Caso 2: Muitas Conexões Abertas

**Diagnóstico**: HttpClient ou conexões de banco não sendo liberadas

**Ação**:
```csharp
// Usar HttpClientFactory
services.AddHttpClient<IPagamentoService, PagamentoService>()
    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

// Configurar pool de conexões do MongoDB
services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(connectionString);
    settings.MaxConnectionPoolSize = 100;
    settings.MinConnectionPoolSize = 10;
    return new MongoClient(settings);
});
```

---

## 8. Duplicação de Pedidos

### Sintomas
- Cliente recebe múltiplas confirmações de pedido
- Múltiplas cobranças no cartão
- Logs mostram mesmo pedido processado 2+ vezes

### Diagnóstico

#### Passo 1: Verificar Idempotência

```bash
# Buscar pedidos duplicados
curl http://localhost:5000/api/pedidos/duplicados
```

```javascript
// Query no MongoDB (se usar persistência MongoDB)
db.PedidoSagaData.aggregate([
    {
        $group: {
            _id: "$ClienteId",
            pedidos: { $push: "$$ROOT" },
            count: { $sum: 1 }
        }
    },
    {
        $match: { count: { $gt: 1 } }
    }
])
```

#### Passo 2: Verificar Logs de Idempotência

```bash
grep "Mensagem duplicada detectada" logs/saga-*.log
```

### Ações Corretivas

#### Caso 1: Idempotência Não Implementada

**Diagnóstico**: Código não verifica `MessageId` antes de processar

**Ação**:
```csharp
// Implementar verificação de idempotência nos handlers
public class ProcessarPagamentoHandler : IHandleMessages<ProcessarPagamento>
{
    private readonly IRepositorioIdempotencia _idempotencia;
    private readonly IBus _bus;

    public async Task Handle(ProcessarPagamento mensagem)
    {
        var chaveIdempotencia = $"pagamento:{mensagem.CorrelacaoId}";

        if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
        {
            _logger.LogWarning("Mensagem duplicada detectada: {CorrelacaoId}",
                mensagem.CorrelacaoId);
            return; // Ignorar mensagem
        }

        // Processar pagamento...
        var resultado = await _servicoPagamento.ProcessarAsync(mensagem);

        await _idempotencia.MarcarProcessadaAsync(chaveIdempotencia,
            TimeSpan.FromHours(24));

        await _bus.Reply(new PagamentoProcessado(...));
    }
}
```

#### Caso 2: Cache de Idempotência Expirou

**Diagnóstico**: TTL do cache muito curto (ex: 5 minutos)

**Ação**:
```csharp
// Aumentar TTL para 24-48 horas
AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48)
```

---

## Scripts de Utilidade

### Limpar SAGAs Antigas

```bash
#!/bin/bash
# cleanup-old-sagas.sh

DAYS_AGO=30

mongo saga-poc --eval "
  db.PedidoSagaData.deleteMany({
    EstadoAtual: { \$in: ['Concluido', 'Compensado'] },
    DataConclusao: {
      \$lt: new Date(Date.now() - $DAYS_AGO * 24 * 60 * 60 * 1000)
    }
  })
"
```

### Health Check Completo

```bash
#!/bin/bash
# health-check-all.sh

SERVICES=("restaurante:5001" "pagamento:5002" "entregador:5003" "orquestrador:5004")

for service in "${SERVICES[@]}"; do
  IFS=':' read -r name port <<< "$service"
  echo "Checking $name..."

  response=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:$port/health)

  if [ "$response" == "200" ]; then
    echo " $name está saudável"
  else
    echo "❌ $name está com problemas (HTTP $response)"
  fi
done
```

---

## Contatos de Escalação

| Severidade | Time | Contato | SLA |
|------------|------|---------|-----|
| P0 - Crítico | On-call Engineering | oncall@empresa.com | 15 min |
| P1 - Alto | Platform Team | platform@empresa.com | 1 hora |
| P2 - Médio | DevOps | devops@empresa.com | 4 horas |
| P3 - Baixo | Squad SAGA | saga-team@empresa.com | 1 dia útil |

---

**Última atualização**: 2026-01-08
**Versão**: 2.0 - Atualizado para Rebus
**Mantenedor**: Equipe Platform Engineering
