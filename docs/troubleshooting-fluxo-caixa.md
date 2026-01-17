# Troubleshooting - Sistema de Fluxo de Caixa

Guia de diagnóstico e resolução de problemas específicos do sistema de Fluxo de Caixa (CQRS + Event-Driven).

---

## Índice

1. [Consolidado Não Está Atualizando](#1-consolidado-não-está-atualizando)
2. [Latência Alta no Consolidado](#2-latência-alta-no-consolidado)
3. [Lançamento Rejeitado](#3-lançamento-rejeitado)
4. [Cache Não Está Funcionando](#4-cache-não-está-funcionando)
5. [Eventos Não Estão Sendo Processados](#5-eventos-não-estão-sendo-processados)
6. [Erro 429 - Rate Limit Excedido](#6-erro-429-rate-limit-excedido)
7. [Dessincronização entre Lançamentos e Consolidado](#7-dessincronização-entre-lançamentos-e-consolidado)
8. [PostgreSQL com Alta Carga](#8-postgresql-com-alta-carga)

---

## 1. Consolidado Não Está Atualizando

### Sintomas

- Lançamento criado com sucesso (202 Accepted)
- Consolidado não reflete o novo lançamento
- Valores totais desatualizados

### Diagnóstico

#### Passo 1: Verificar se o Lançamento Foi Persistido

```bash
# Via API
curl http://localhost:5000/api/lancamentos/{id}

# Ou via PostgreSQL
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_lancamentos \
  -c "SELECT * FROM lancamentos WHERE id = '{id}';"
```

#### Passo 2: Verificar Eventos no RabbitMQ

```bash
# Acessar Management UI
http://localhost:15672

# Verificar fila fluxocaixa-consolidado
# Deve ter mensagens prontas (Ready) ou em processamento (Unacked)
```

#### Passo 3: Verificar Logs do Serviço Consolidado

```bash
# Se rodando via Docker
docker logs fluxocaixa-consolidado --tail 100

# Procurar por erros
docker logs fluxocaixa-consolidado 2>&1 | grep -i "error\|exception"
```

#### Passo 4: Verificar se o Serviço Consolidado Está Rodando

```bash
# Verificar containers
docker ps | grep consolidado

# Verificar health (se implementado)
curl http://localhost:5002/health
```

### Ações Corretivas

#### Caso 1: Serviço Consolidado Parado

**Diagnóstico**: `docker ps` não mostra o container consolidado

**Ação**:
```bash
# Reiniciar serviço
docker-compose restart fluxocaixa-consolidado

# Ou se rodando manualmente
cd src/SagaPoc.FluxoCaixa.Consolidado
dotnet run
```

#### Caso 2: Eventos na Dead Letter Queue

**Diagnóstico**: Mensagens na fila `fluxocaixa-consolidado-dlq`

**Ação**:
```bash
# 1. Ver mensagem na DLQ
curl -u saga:saga123 -X POST \
  http://localhost:15672/api/queues/%2F/fluxocaixa-consolidado-dlq/get \
  -H "content-type: application/json" \
  -d '{"count":1,"ackmode":"ack_requeue_false","encoding":"auto"}'

# 2. Analisar motivo do erro (campo x-exception-message)

# 3. Corrigir o problema (código/config)

# 4. Mover mensagens de volta à fila principal (via Shovel ou manualmente)
```

#### Caso 3: Erro de Conexão com PostgreSQL

**Diagnóstico**: Logs mostram "Connection refused" ou "Unable to connect"

**Ação**:
```bash
# 1. Verificar se PostgreSQL está rodando
docker ps | grep postgres

# 2. Verificar porta correta (Consolidado usa 5434)
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_consolidado -c "\dt"

# 3. Verificar string de conexão no appsettings.json
cat src/SagaPoc.FluxoCaixa.Consolidado/appsettings.json | grep ConsolidadoDb

# 4. Reiniciar serviço após correção
```

---

## 2. Latência Alta no Consolidado

### Sintomas

- Consultas ao consolidado demorando > 100ms
- Timeout em requisições
- Usuários reclamando de lentidão

### Diagnóstico

#### Passo 1: Verificar Cache Hit Rate

```bash
# Verificar headers de resposta
curl -I http://localhost:5000/api/consolidado/COM001/2026-01-15

# Procurar por:
# X-Cache-Status: HIT-L1 (ideal)
# X-Cache-Status: HIT-L2 (ok)
# X-Cache-Status: MISS (ruim se frequente)
```

#### Passo 2: Verificar Redis

```bash
# Verificar se Redis está rodando
docker ps | grep redis

# Conectar ao Redis
docker exec -it redis redis-cli

# Verificar chaves
127.0.0.1:6379> KEYS FluxoCaixa:*

# Verificar TTL de uma chave
127.0.0.1:6379> TTL FluxoCaixa:consolidado:COM001:2026-01-15

# Ver estatísticas
127.0.0.1:6379> INFO stats
```

#### Passo 3: Verificar Performance do PostgreSQL

```bash
# Executar query diretamente e medir tempo
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_consolidado \
  -c "\timing on" \
  -c "SELECT * FROM consolidado_diario WHERE comerciante = 'COM001' AND data = '2026-01-15';"
```

### Ações Corretivas

#### Caso 1: Redis Parado ou Inacessível

**Diagnóstico**: Logs mostram "Unable to connect to Redis"

**Ação**:
```bash
# 1. Verificar se Redis está rodando
docker-compose restart redis

# 2. Verificar conectividade
docker exec -it fluxocaixa-api ping redis

# 3. Verificar string de conexão
# appsettings.json → ConnectionStrings:Redis

# 4. Aguardar cache popular novamente (5-10 minutos)
```

#### Caso 2: Cache Miss Frequente

**Diagnóstico**: Maioria das requisições retorna `X-Cache-Status: MISS`

**Ação**:
```bash
# 1. Verificar TTL configurado (pode estar muito curto)
# Program.cs → AddMemoryCache / AddStackExchangeRedisCache

# 2. Verificar se cache está sendo invalidado desnecessariamente
docker logs fluxocaixa-consolidado | grep "Invalidando cache"

# 3. Aumentar TTL se necessário (ex: 1min → 5min no L1)
```

#### Caso 3: Query SQL Lenta

**Diagnóstico**: Query no PostgreSQL demora > 50ms

**Ação**:
```sql
-- 1. Verificar se índices existem
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename = 'consolidado_diario';

-- 2. Criar índices faltantes
CREATE INDEX IF NOT EXISTS idx_consolidado_comerciante_data
    ON consolidado_diario(comerciante, data DESC);

-- 3. Analisar plano de execução
EXPLAIN ANALYZE
SELECT * FROM consolidado_diario
WHERE comerciante = 'COM001' AND data = '2026-01-15';

-- 4. Atualizar estatísticas
ANALYZE consolidado_diario;
```

---

## 3. Lançamento Rejeitado

### Sintomas

- API retorna 400 Bad Request
- Mensagem de erro: "Lançamento rejeitado"

### Diagnóstico

#### Passo 1: Verificar Payload

```bash
# Ver body da requisição
curl -X POST http://localhost:5000/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": 2,
    "valor": -150.00,  # ERRO: valor negativo
    "dataLancamento": "2026-01-15",
    "descricao": "Venda",
    "comerciante": "COM001"
  }' -v
```

#### Passo 2: Ver Mensagem de Erro Detalhada

A resposta 400 deve conter detalhes:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Valor": ["O valor deve ser maior que zero"]
  }
}
```

### Ações Corretivas

#### Caso 1: Validação de Dados

**Erros Comuns**:
- `valor`: Deve ser maior que zero
- `tipo`: Deve ser 1 (Débito) ou 2 (Crédito)
- `dataLancamento`: Formato inválido (use yyyy-MM-dd)
- `descricao`: Vazio ou maior que 500 caracteres
- `comerciante`: Vazio ou maior que 100 caracteres

**Ação**: Corrigir payload e reenviar

#### Caso 2: Regras de Negócio

Verificar logs do serviço de Lançamentos:
```bash
docker logs fluxocaixa-lancamentos --tail 50 | grep "Validação falhou"
```

---

## 4. Cache Não Está Funcionando

### Sintomas

- Todas as requisições retornam `X-Cache-Status: MISS`
- Latência sempre alta (~100ms)
- Cache Hit Rate = 0%

### Diagnóstico

#### Passo 1: Verificar Headers de Cache

```bash
curl -I http://localhost:5000/api/consolidado/COM001/2026-01-15
```

Esperado:
```
X-Cache-Status: HIT-L1
Cache-Control: public, max-age=60
Age: 45
```

#### Passo 2: Verificar Logs de Cache

```bash
docker logs fluxocaixa-api | grep -i cache
```

#### Passo 3: Verificar Configuração

```bash
# Ver configuração do Redis
cat src/SagaPoc.FluxoCaixa.Api/appsettings.json | grep Redis

# Ver configuração do Memory Cache
cat src/SagaPoc.FluxoCaixa.Api/Program.cs | grep AddMemoryCache -A 5
```

### Ações Corretivas

#### Caso 1: Cache Desabilitado

**Diagnóstico**: `AddMemoryCache()` ou `AddResponseCaching()` comentados

**Ação**: Descomentar e reiniciar aplicação

#### Caso 2: Redis com Problema

```bash
# Testar conexão com Redis
docker exec -it fluxocaixa-api sh
nc -zv redis 6379

# Se falhar, verificar:
# 1. Redis está rodando?
docker ps | grep redis

# 2. String de conexão correta?
# appsettings.json → ConnectionStrings:Redis
```

---

## 5. Eventos Não Estão Sendo Processados

### Sintomas

- Mensagens acumulando na fila `fluxocaixa-consolidado`
- Nenhum consumo de mensagens

### Diagnóstico

#### Passo 1: Verificar Filas no RabbitMQ

```bash
# Acessar Management UI
http://localhost:15672

# Verificar fila fluxocaixa-consolidado
# Ready: Número de mensagens aguardando processamento
# Consumers: Deve ser > 0
```

#### Passo 2: Verificar Consumidores

```bash
# No RabbitMQ Management UI:
# Queues → fluxocaixa-consolidado → Consumers

# Deve mostrar:
# - Consumer tag
# - Channel
# - Connection
```

### Ações Corretivas

#### Caso 1: Nenhum Consumidor Ativo

**Diagnóstico**: `Consumers: 0`

**Ação**:
```bash
# Verificar se serviço Consolidado está rodando
docker ps | grep consolidado

# Se não estiver, iniciar
docker-compose up -d fluxocaixa-consolidado

# Verificar logs
docker logs fluxocaixa-consolidado
```

#### Caso 2: Mensagens na DLQ

Ver seção [Consolidado Não Está Atualizando - Caso 2](#caso-2-eventos-na-dead-letter-queue)

---

## 6. Erro 429 - Rate Limit Excedido

### Sintomas

- API retorna 429 Too Many Requests
- Mensagem: "Rate limit exceeded"

### Diagnóstico

```bash
# Fazer múltiplas requisições rápidas
for i in {1..60}; do
  curl -w "%{http_code}\n" http://localhost:5000/api/consolidado/COM001/2026-01-15 &
done
```

Se configurado para 50 req/s, a partir da 51ª requisição no mesmo segundo retornará 429.

### Ações Corretivas

#### Caso 1: Carga Legítima Excedeu Limite

**Ação**: Aumentar limite ou implementar estratégia de retry com backoff no cliente

```csharp
// Program.cs
options.AddFixedWindowLimiter("consolidado", opt =>
{
    opt.PermitLimit = 100;  // Aumentar de 50 para 100
    opt.Window = TimeSpan.FromSeconds(1);
});
```

#### Caso 2: Cliente Não Está Respeitando Rate Limit

**Ação**: Implementar retry com backoff exponencial no cliente

```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => r.StatusCode == HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

---

## 7. Dessincronização entre Lançamentos e Consolidado

### Sintomas

- Lançamentos existem no banco de Lançamentos
- Consolidado não reflete esses lançamentos
- Valores totais incorretos

### Diagnóstico

#### Passo 1: Comparar Dados

```sql
-- Banco de Lançamentos
SELECT
    comerciante,
    data_lancamento,
    SUM(CASE WHEN tipo = 'Credito' THEN valor ELSE 0 END) AS total_creditos,
    SUM(CASE WHEN tipo = 'Debito' THEN valor ELSE 0 END) AS total_debitos
FROM lancamentos
WHERE comerciante = 'COM001' AND data_lancamento = '2026-01-15'
GROUP BY comerciante, data_lancamento;

-- Banco de Consolidado
SELECT * FROM consolidado_diario
WHERE comerciante = 'COM001' AND data = '2026-01-15';
```

#### Passo 2: Verificar Eventos Perdidos

```bash
# Ver eventos publicados (logs do Lançamentos)
docker logs fluxocaixa-lancamentos | grep "Evento publicado"

# Ver eventos consumidos (logs do Consolidado)
docker logs fluxocaixa-consolidado | grep "Evento recebido"
```

### Ações Corretivas

#### Caso 1: Eventos Foram Perdidos

**Ação**: Reconciliação manual via job batch

```csharp
// Script de reconciliação (executar manualmente)
POST /api/admin/reconciliar
{
    "dataInicio": "2026-01-01",
    "dataFim": "2026-01-31"
}
```

#### Caso 2: Processamento Parcial

**Ação**: Reprocessar eventos específicos

```bash
# 1. Identificar lançamentos não processados
# 2. Republicar eventos manualmente
# 3. Aguardar processamento
```

---

## 8. PostgreSQL com Alta Carga

### Sintomas

- CPU do PostgreSQL > 80%
- Queries lentas
- Timeouts frequentes

### Diagnóstico

```bash
# Ver processos ativos
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_consolidado \
  -c "SELECT pid, usename, application_name, state, query
      FROM pg_stat_activity
      WHERE state != 'idle'
      ORDER BY query_start;"

# Ver queries lentas
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_consolidado \
  -c "SELECT query, calls, total_time, mean_time
      FROM pg_stat_statements
      ORDER BY mean_time DESC
      LIMIT 10;"
```

### Ações Corretivas

#### Caso 1: Índices Faltando

```sql
-- Criar índices necessários
CREATE INDEX CONCURRENTLY idx_consolidado_comerciante_data
    ON consolidado_diario(comerciante, data DESC);

-- Verificar uso de índices
EXPLAIN ANALYZE
SELECT * FROM consolidado_diario
WHERE comerciante = 'COM001';
```

#### Caso 2: Muitas Conexões Abertas

```sql
-- Ver conexões ativas
SELECT count(*) FROM pg_stat_activity;

-- Configurar pool de conexões no appsettings.json
"FluxoCaixaDb": "Host=localhost;Port=5433;...;MaxPoolSize=50;MinPoolSize=5"
```

---

## Scripts de Utilidade

### Verificar Saúde Completa do Sistema

```bash
#!/bin/bash
# health-check-fluxocaixa.sh

echo "=== Verificando Serviços ==="
docker ps | grep -E "fluxocaixa|postgres|rabbitmq|redis"

echo "\n=== Verificando PostgreSQL ==="
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_lancamentos -c "\dt"
docker exec -it postgres-fluxocaixa psql -U saga -d fluxocaixa_consolidado -c "\dt"

echo "\n=== Verificando Redis ==="
docker exec -it redis redis-cli ping

echo "\n=== Verificando RabbitMQ ==="
curl -s -u saga:saga123 http://localhost:15672/api/queues | \
  jq '.[] | select(.name | contains("fluxocaixa")) | {name, messages}'

echo "\n=== Verificando API ==="
curl -s http://localhost:5000/health | jq '.'
```

### Limpar Cache Manualmente

```bash
#!/bin/bash
# clear-cache.sh

echo "Limpando Redis..."
docker exec -it redis redis-cli FLUSHALL

echo "Reiniciando API (para limpar Memory Cache)..."
docker-compose restart fluxocaixa-api

echo "Cache limpo!"
```

---

## Contatos de Escalação

| Severidade | Time | Contato | SLA |
|------------|------|---------|-----|
| P0 - Sistema Fora | On-call Engineering | oncall@empresa.com | 15 min |
| P1 - Degradação Severa | Platform Team | platform@empresa.com | 1 hora |
| P2 - Performance Degradada | DevOps | devops@empresa.com | 4 horas |
| P3 - Questões Gerais | Squad FluxoCaixa | fluxocaixa-team@empresa.com | 1 dia útil |

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
**Mantenedor**: Equipe de Arquitetura
