# Runbook do fluxo de caixa

Diagnóstico do contexto de fluxo de caixa (CQRS: lançamentos → eventos → consolidado) na stack do compose. Todos os comandos rodam no Git Bash a partir da raiz do repositório e usam só `docker`, `curl`, `grep` e `awk`. Nomes de container, portas e bancos estão em [docker/README.md](../docker/README.md).

O caminho de um lançamento:

1. `lancamentos-api` (5200) recebe `POST /api/lancamentos`, valida o payload, envia `RegistrarLancamento` para a fila `fluxocaixa-lancamentos` e responde **202**.
2. `lancamentos-worker` grava o lançamento em `fluxocaixa_lancamentos` e publica `LancamentoCreditoRegistrado` ou `LancamentoDebitoRegistrado` no exchange `RebusTopics`.
3. O RabbitMQ entrega o evento à fila `fluxocaixa-consolidado`, **desde que o Consolidado tenha assinado o tópico**. Sem a assinatura, o evento é descartado em silêncio.
4. `fluxocaixa-consolidado` atualiza a linha do dia em `fluxocaixa_consolidado` (uma mensagem por vez) e apaga a chave do dia no Redis.
5. `fluxocaixa-api` (5100) responde `GET /api/consolidado/{comerciante}/{data}`, com cache.

Como na SAGA, o 202 não prova nada. O script `bash docs/scripts/testar-fluxo-caixa.sh` percorre esse caminho e confere o saldo.

---

## Índice

1. [Verificação rápida](#1-verificação-rápida)
2. [Consolidado não atualiza](#2-consolidado-não-atualiza)
3. [Consulta devolve valor antigo](#3-consulta-devolve-valor-antigo)
4. [Erros da API de lançamentos (400)](#4-erros-da-api-de-lançamentos-400)
5. [Erros da API de consulta (404 e 503)](#5-erros-da-api-de-consulta-404-e-503)
6. [Mensagem na fila error](#6-mensagem-na-fila-error)
7. [Lançamentos e consolidado divergentes](#7-lançamentos-e-consolidado-divergentes)

---

## 1. Verificação rápida

```bash
# APIs, filas e as duas assinaturas do Consolidado (sai 1 e lista o que falta)
bash docs/scripts/aguardar-stack.sh

# Health das duas APIs
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5200/health
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5100/health

# Filas do fluxo de caixa e da fila de erro
docker exec saga-rabbitmq rabbitmqctl list_queues -q name messages_ready messages_unacknowledged consumers \
  | grep -E 'fluxocaixa|error'

# Assinaturas: devem aparecer as duas linhas, com destino fluxocaixa-consolidado
docker exec saga-rabbitmq rabbitmqctl list_bindings -q source_name destination_name routing_key | grep RebusTopics

# Redis
docker exec fluxocaixa-redis redis-cli ping
```

O worker e o Consolidado não têm porta nem `/health`; o sinal de vida deles é o consumidor (`consumers` = 1) nas filas `fluxocaixa-lancamentos` e `fluxocaixa-consolidado`.

---

## 2. Consolidado não atualiza

### Sintoma

O `POST` devolveu 202, mas a consulta do dia não mostra o lançamento (404 ou saldo antigo). Se o lançamento acabou de ser feito, leia antes a [seção 3](#3-consulta-devolve-valor-antigo): a consulta pode estar só defasada.

### Diagnóstico, na ordem do caminho

**Passo 1: o lançamento foi gravado?**

```bash
curl -s "http://localhost:5200/api/lancamentos?comerciante=<comerciante>&inicio=<yyyy-MM-dd>&fim=<yyyy-MM-dd>"
```

Lista vazia: o worker não gravou. Confira a fila `fluxocaixa-lancamentos` (mensagem parada com `consumers` = 0 significa worker fora do ar) e o log:

```bash
docker logs lancamentos-worker --since 30m 2>&1 | grep -E 'RegistrarLancamento|Falha ao criar|Erro ao gravar'
```

`Falha ao criar lancamento` indica que o domínio recusou o lançamento (por exemplo, tipo fora do enum ou valor acima de 1 bilhão; a API confere as duas regras, então isso indica comando enviado direto para a fila). O worker registra o aviso e descarta a mensagem: ela não vai para a `error`.

**Passo 2: o evento foi publicado?**

```bash
docker logs lancamentos-worker --since 30m 2>&1 | grep 'Publicando evento'
```

**Passo 3: o evento chegou ao Consolidado?** Sem as duas assinaturas no `RebusTopics` (comando da seção 1), o evento publicado no passo 2 se perde sem erro. A assinatura é feita pelo Consolidado ao subir; se ela faltar, suba o container de novo (`docker compose -f docker/docker-compose.yml up -d fluxocaixa-consolidado`) e confira com `aguardar-stack.sh`. Os eventos descartados antes disso não voltam: repita os lançamentos.

```bash
docker logs fluxocaixa-consolidado --since 30m 2>&1 | grep -E 'Processando (credito|debito)|Consolidado atualizado|Erro ao processar'
```

**Passo 4: a linha do dia foi gravada?**

```bash
docker exec fluxocaixa-postgres-consolidado psql -U saga -d fluxocaixa_consolidado -c \
  "SELECT \"Data\", \"TotalCreditos\", \"TotalDebitos\", \"QuantidadeCreditos\", \"QuantidadeDebitos\", \"UltimaAtualizacao\"
   FROM \"ConsolidadosDiarios\" WHERE \"Comerciante\" = '<comerciante>' ORDER BY \"Data\" DESC LIMIT 5;"
```

Se a linha está certa no banco e a API devolve outro valor, o problema é cache ([seção 3](#3-consulta-devolve-valor-antigo)).

### Ação

- Container fora do ar: `docker logs <container> --tail 100` para ver a exceção da subida e `docker compose -f docker/docker-compose.yml up -d --build <serviço>`. As mensagens que ficaram na fila são consumidas quando ele volta.
- Erro de conexão com banco ou Redis no log: os serviços usam os nomes do compose (`postgres-fluxocaixa`, `postgres-consolidado`, `redis`), não `localhost`. Confira as connection strings no `docker-compose.yml`.

---

## 3. Consulta devolve valor antigo

### Por que acontece

A API de consulta tem três camadas de cache, e o Consolidado só invalida uma delas:

| Camada | Duração | Invalidada pelo Consolidado? |
|---|---|---|
| Response cache do ASP.NET Core (`Cache-Control: public,max-age=60`) | 60 s | não |
| Memory cache da API | 1 min | não |
| Redis (`FluxoCaixa:consolidado:<comerciante>:<yyyy-MM-dd>`) | 5 min | sim, a cada evento |

Por isso a leitura pode vir defasada **até cerca de 60 s** depois de o consolidado ser gravado. É uma limitação conhecida, não um defeito do momento; o script de teste contorna usando um comerciante novo por cenário.

### Diagnóstico

```bash
# Chaves do consolidado no Redis (são hashes do IDistributedCache; o JSON fica no campo "data")
docker exec fluxocaixa-redis redis-cli --scan --pattern 'FluxoCaixa:consolidado:*'
docker exec fluxocaixa-redis redis-cli hget 'FluxoCaixa:consolidado:<comerciante>:<yyyy-MM-dd>' data
docker exec fluxocaixa-redis redis-cli ttl 'FluxoCaixa:consolidado:<comerciante>:<yyyy-MM-dd>'
```

Compare com a linha do banco (passo 4 da seção 2). Banco atualizado e chave ausente no Redis significam que só os caches da própria API estão defasados.

### Ação

Esperar cerca de 1 minuto. Se o valor não mudar depois disso, não é cache: volte à [seção 2](#2-consolidado-não-atualiza).

---

## 4. Erros da API de lançamentos (400)

A validação do payload é feita por atributos no `RegistrarLancamentoRequest`, e a resposta 400 é o `ValidationProblemDetails` padrão do ASP.NET Core, com o campo e a mensagem:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Valor": ["O valor deve ser maior que zero"] }
}
```

| Campo | Regra |
|---|---|
| `tipo` | `1` = débito, `2` = crédito |
| `valor` | maior ou igual a 0,01 |
| `descricao` | obrigatória, de 3 a 500 caracteres |
| `comerciante` | obrigatório, de 3 a 100 caracteres |
| `categoria` | opcional, até 100 caracteres |
| `dataLancamento` | opcional (padrão: hoje), no formato `yyyy-MM-dd` |

Payload com acento enviado direto no argumento do `curl` chega corrompido no Git Bash do Windows. Grave o JSON em arquivo e envie com `--data-binary`:

```bash
printf '{"tipo":2,"valor":150.00,"dataLancamento":"2026-10-20","descricao":"Venda balcão","comerciante":"COM001","categoria":"Vendas"}' > lancamento.json
curl -s -H 'Content-Type: application/json' --data-binary @lancamento.json http://localhost:5200/api/lancamentos
```

---

## 5. Erros da API de consulta (404 e 503)

- **404 em `GET /api/consolidado/{comerciante}/{data}`:** não existe linha para aquele comerciante e dia. Ou nenhum lançamento foi feito, ou ele ainda não passou pelo Consolidado ([seção 2](#2-consolidado-não-atualiza)). A data vai na rota (`/api/consolidado/COM001/2026-10-20`); para um intervalo, use `/api/consolidado/COM001/periodo?inicio=2026-10-01&fim=2026-10-20`.
- **503 em rajada de requisições:** os controllers da API de consulta têm rate limit de janela fixa (50 requisições por segundo e fila de 10). Excedido o limite, o ASP.NET Core responde 503, o código padrão do rate limiter, já que o projeto não configura outro. O `/health` fica fora do limite.

---

## 6. Mensagem na fila error

Mensagem na `error` é sempre bug. O procedimento para ler a mensagem sem tirá-la da fila, achar a fila de origem (`rbs2-source-queue`) e a exceção (`rbs2-error-details`) é o mesmo da SAGA: [runbook da SAGA, seção 3](runbook-troubleshooting.md#3-mensagem-na-fila-error). Logs a conferir, conforme a origem:

```bash
docker logs lancamentos-worker --since 30m 2>&1 | grep -iE 'erro|exception'
docker logs fluxocaixa-consolidado --since 30m 2>&1 | grep -iE 'erro|exception'
```

Só purgue a fila (`docker exec saga-rabbitmq rabbitmqctl purge_queue error`) depois de corrigir a causa e conferir que as mensagens são de teste da própria sessão.

Cuidado ao reprocessar mensagens de `fluxocaixa-lancamentos`: o worker não tem idempotência. Cada tentativa de `RegistrarLancamento` gera um `Id` novo, então uma mensagem que falhou depois do commit grava o lançamento de novo (item do backlog).

---

## 7. Lançamentos e consolidado divergentes

Os dois lados ficam em bancos diferentes, então a comparação é feita em duas consultas:

```bash
# Lado da escrita: soma por dia
docker exec fluxocaixa-postgres-lancamentos psql -U saga -d fluxocaixa_lancamentos -c \
  "SELECT data_lancamento,
          SUM(CASE WHEN tipo = 'Credito' THEN valor ELSE 0 END) AS creditos,
          SUM(CASE WHEN tipo = 'Debito' THEN valor ELSE 0 END) AS debitos,
          COUNT(*) AS quantidade
   FROM lancamentos WHERE comerciante = '<comerciante>' GROUP BY data_lancamento ORDER BY data_lancamento DESC;"

# Lado do consolidado
docker exec fluxocaixa-postgres-consolidado psql -U saga -d fluxocaixa_consolidado -c \
  "SELECT \"Data\"::date, \"TotalCreditos\", \"TotalDebitos\", \"QuantidadeCreditos\" + \"QuantidadeDebitos\" AS quantidade
   FROM \"ConsolidadosDiarios\" WHERE \"Comerciante\" = '<comerciante>' ORDER BY \"Data\" DESC;"
```

Causas possíveis, todas já descritas acima:

- **Consolidado com menos:** evento descartado por falta de assinatura (seção 2, passo 3) ou parado na `error` (seção 6).
- **Lançamentos com mais do que foi enviado:** reprocessamento no worker sem idempotência (seção 6).

Não há rotina de reconciliação: para acertar um ambiente de teste, apague os volumes (`docker compose -f docker/docker-compose.yml down -v`) e suba de novo.

### Logs e traces

O Consolidado não manda log para o Seq nem gera trace; use `docker logs fluxocaixa-consolidado`. No Jaeger (http://localhost:16686) aparecem `SagaPoc.Lancamentos.Api` e `SagaPoc.Lancamentos.Worker`. No Seq (http://localhost:5342) aparecem os logs das duas APIs; o worker de lançamentos também não manda log para o Seq.
