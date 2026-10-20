# Runbook da SAGA de pedidos

Diagnóstico dos problemas que acontecem de fato nesta stack (compose local). Todos os comandos rodam no Git Bash a partir da raiz do repositório e usam só `docker`, `curl`, `grep` e `awk`; nenhuma ferramenta extra precisa ser instalada. Nomes de container, portas e consultas SQL de apoio estão em [docker/README.md](../docker/README.md).

Duas regras valem para todo o documento:

- **O 202 da API não prova nada.** `POST /api/pedidos` responde assim que envia `IniciarPedido` para a fila. Quem diz como a SAGA terminou é o log `[SAGA]` do orquestrador.
- **`GET /api/pedidos/{id}/status` é mock** e responde sempre `EmProcessamento`. Não use esse endpoint para diagnóstico.

---

## Índice

1. [Verificação rápida da stack](#1-verificação-rápida-da-stack)
2. [Pedido aceito, mas a SAGA não termina](#2-pedido-aceito-mas-a-saga-não-termina)
3. [Mensagem na fila error](#3-mensagem-na-fila-error)
4. [Compensação que não termina](#4-compensação-que-não-termina)
5. [Pedido cancelado ou compensado sem motivo aparente](#5-pedido-cancelado-ou-compensado-sem-motivo-aparente)
6. [Logs e traces faltando](#6-logs-e-traces-faltando)

---

## 1. Verificação rápida da stack

```bash
# APIs, filas e assinaturas prontas (sai 1 e lista o que falta se o tempo acabar)
bash docs/scripts/aguardar-stack.sh

# Containers e há quanto tempo estão no ar (reinício recente indica queda)
docker compose -f docker/docker-compose.yml ps

# Filas: mensagens prontas, em processamento e consumidores
docker exec saga-rabbitmq rabbitmqctl list_queues -q name messages_ready messages_unacknowledged consumers
```

Estado saudável, sem pedidos em andamento: todas as filas `fila-*` com `0 0 1` (nada acumulado, um consumidor) e a fila `error` com `0`. Só `saga-api` tem `/health` (`curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5000/health`); o orquestrador e os serviços da SAGA são workers sem porta, e o sinal de vida deles é o consumidor na fila.

---

## 2. Pedido aceito, mas a SAGA não termina

### Sintoma

`POST /api/pedidos` devolveu 202 com um `pedidoId`, mas o log do orquestrador não mostra `CONCLUÍDO COM SUCESSO` nem `COMPENSAÇÃO CONCLUÍDA` para ele.

### Diagnóstico

**Passo 1: o que o orquestrador registrou.**

```bash
docker logs saga-orquestrador 2>&1 | grep '\[SAGA\]' | grep '<pedidoId>'
```

A última linha diz em que passo a SAGA parou: `Validação restaurante`, `Pagamento`, `Entregador`, ou nenhuma linha (o orquestrador nem recebeu o pedido).

**Passo 2: estado persistido.** SAGA em andamento fica em `pedido_saga_data`; concluída ou compensada é removida pelo Rebus (`MarkAsComplete()`).

```bash
docker exec saga-postgres psql -U saga -d sagapoc -c \
  "SELECT id, convert_from(data, 'UTF8')::json->>'EstadoAtual' AS estado FROM pedido_saga_data WHERE id = '<pedidoId>';"
```

| Resultado | Leitura |
|---|---|
| nenhuma linha e log com desfecho | a SAGA terminou; o problema está em quem consultou |
| nenhuma linha e nenhum log `[SAGA]` | `IniciarPedido` não chegou ao orquestrador (passo 3) |
| `ValidandoRestaurante`, `ProcessandoPagamento`, `AlocandoEntregador` ou `NotificandoCliente` | a resposta do serviço daquele passo não voltou (passo 3) |
| `ExecutandoCompensacao` | ver a [seção 4](#4-compensação-que-não-termina) |

**Passo 3: fila do passo travado.**

```bash
docker exec saga-rabbitmq rabbitmqctl list_queues -q name messages_ready messages_unacknowledged consumers
```

- `messages_ready` > 0 com `consumers` = 0: o serviço daquela fila caiu. `docker compose -f docker/docker-compose.yml ps` e `docker logs <container> --tail 100` mostram a exceção.
- Fila vazia e mensagem na `error`: ver a [seção 3](#3-mensagem-na-fila-error).
- Tudo vazio e nenhum log `[SAGA]`: a API não conseguiu enviar. Procure o `pedidoId` no log da API (`docker logs saga-api 2>&1 | grep '<pedidoId>'`).

### Ação

Subir de novo o serviço que caiu, depois de entender a exceção no log:

```bash
docker compose -f docker/docker-compose.yml up -d --build <serviço>
```

A mensagem que estava na fila é consumida quando o serviço volta; a SAGA continua do ponto em que parou. Não edite `pedido_saga_data` nem `pedido_saga_index` à mão: as duas tabelas são do Rebus.

---

## 3. Mensagem na fila error

### Sintoma

`rabbitmqctl list_queues` mostra `error` com mensagens. Nenhum dos 14 casos de uso deveria levar mensagem para lá: **é sempre bug**.

O Rebus tenta entregar cada mensagem algumas vezes (o padrão da biblioteca; o projeto não muda essa configuração) e, se o handler lançar exceção em todas, move a mensagem para `error` com cabeçalhos que dizem de onde ela veio e por quê.

### Diagnóstico

**Passo 1: ler a mensagem sem tirá-la da fila.** Pela UI (http://localhost:15672, `saga`/`saga123`): Queues → `error` → Get messages, com "Ack mode" em "Nack message requeue true". Ou pela API de management:

```bash
curl -s -u saga:saga123 -H 'content-type: application/json' -X POST \
  -d '{"count":1,"ackmode":"ack_requeue_true","encoding":"auto"}' \
  http://localhost:15672/api/queues/%2F/error/get > erro.json

# Fila de origem e começo da exceção
grep -o '"rbs2-source-queue":"[^"]*"' erro.json
grep -o '"rbs2-error-details":"[^"]\{0,400\}' erro.json
```

**Passo 2: log do serviço dono da fila de origem**, no horário da falha. O handler loga a exceção com o identificador da operação:

```bash
docker logs saga-servico-pagamento --since 30m 2>&1 | grep -iE 'erro|exception' | tail -20
```

### Ação

1. Corrigir a causa no código e subir de novo o serviço (`docker compose -f docker/docker-compose.yml up -d --build <serviço>`).
2. Repetir o cenário (`bash docs/scripts/testar-casos-de-uso.sh <caso>`) e conferir que a fila `error` não recebe nada novo.
3. Só então purgar a fila, e só se as mensagens forem de teste da própria sessão:

```bash
docker exec saga-rabbitmq rabbitmqctl purge_queue error
```

Não há shovel nem reenvio automático da `error` configurados nesta stack. Para reprocessar uma mensagem específica, use "Move messages" na UI (exige o plugin `rabbitmq_shovel_management`, que não vem habilitado) ou repita o pedido.

---

## 4. Compensação que não termina

### Sintoma

O log mostra `COMPENSAÇÃO INICIADA` para o pedido e termina em `COMPENSAÇÃO FALHOU` (nível de erro), ou ainda não terminou e a linha continua em `pedido_saga_data` com `EstadoAtual` = `ExecutandoCompensacao`.

### Como a compensação funciona aqui

A SAGA envia os comandos de compensação dos passos que deram certo (`EstornarPagamento`, `CancelarPedidoRestaurante`, `LiberarEntregador`) e confere o `Sucesso` de cada resposta. Recusa gera nova tentativa (até 3, com 2 s entre elas). Se a resposta não volta, o prazo da compensação (30 s) encerra a SAGA. Nos dois casos, o que não foi compensado aparece na linha `COMPENSAÇÃO FALHOU`, em `Sem compensação:` e `Erros:`.

### Diagnóstico

```bash
# SAGA ainda em compensação: passos que esperam resposta e os já compensados
docker exec saga-postgres psql -U saga -d sagapoc -c \
  "SELECT convert_from(data, 'UTF8')::json->>'PassosPendentes' AS pendentes,
          convert_from(data, 'UTF8')::json->>'PassosCompensados' AS compensados,
          convert_from(data, 'UTF8')::json->>'PagamentoProcessado' AS pagamento,
          convert_from(data, 'UTF8')::json->>'RestauranteValidado' AS restaurante,
          convert_from(data, 'UTF8')::json->>'EntregadorAlocado' AS entregador
   FROM pedido_saga_data WHERE id = '<pedidoId>';"
```

O passo em `PassosPendentes` é o que falta. Para SAGA já encerrada, a linha `COMPENSAÇÃO FALHOU` do orquestrador traz a mesma informação. Confira a fila desse serviço (`fila-pagamento`, `fila-restaurante` ou `fila-entregador`), a fila `error` e o log do serviço, como nas seções 2 e 3.

### Ação

Mesma da seção 2: subir o serviço que caiu ou corrigir o bug que levou o comando para a `error`. Com a SAGA encerrada como `COMPENSAÇÃO FALHOU`, a compensação que faltou precisa ser feita à mão no serviço (por exemplo, o estorno da transação listada no log). A idempotência dos handlers de estorno e de liberação de entregador fica em memória (`RepositorioIdempotenciaInMemory`): ela se perde quando o container reinicia, então um comando reprocessado depois de um reinício é executado de novo.

---

## 5. Pedido cancelado ou compensado sem motivo aparente

Antes de tratar como bug, confira as regras dos serviços simulados em [casos-uso.md](casos-uso.md#regras-dos-serviços-simulados). As que mais confundem:

- **Horário de pico:** entre 12h e 14h59 UTC, o entregador recusa 30% das alocações com `HORARIO_PICO`, e um pedido que deveria concluir termina compensado. O script de casos de uso marca isso como AVISO. Para desligar:

  ```bash
  SIMULAR_HORARIO_PICO=false docker compose -f docker/docker-compose.yml up -d saga-servico-entregador
  ```

- **Pool de entregadores:** são 10; cada um volta ao pool quando a entrega simulada termina (1 minuto estimado = 1 segundo). Uma rajada de pedidos esgota o pool e gera `SEM_ENTREGADOR_DISPONIVEL`.
- **Estado em memória:** transações, cancelamentos e alocações dos serviços simulados se perdem quando o container reinicia.

O motivo da recusa aparece no log do orquestrador (`[SAGA] Pedido <id> - Motivo: ...` ou `- Cancelado: ...`).

---

## 6. Logs e traces faltando

| Onde | O que aparece |
|---|---|
| `docker logs <container>` | todos os serviços; é a fonte confiável |
| Seq (http://localhost:5342) | só as APIs (`saga-api`, `lancamentos-api`, `fluxocaixa-api`). Os workers (orquestrador e serviços da SAGA) não mandam log para o Seq: a configuração do Serilog dos hosts lê só o `appsettings.json` |
| Jaeger (http://localhost:16686) | só `SagaPoc.Api`, `SagaPoc.Lancamentos.Api` e `SagaPoc.Lancamentos.Worker`. O orquestrador e os serviços da SAGA não geram span (o Rebus não tem instrumentação configurada) |

Os dois buracos estão no backlog do projeto. Até lá, rastreie um pedido pelo `pedidoId`, que é o `CorrelacaoId` de todas as mensagens da SAGA:

```bash
for c in saga-api saga-orquestrador saga-servico-restaurante saga-servico-pagamento saga-servico-entregador saga-servico-notificacao; do
  echo "== $c"; docker logs "$c" 2>&1 | grep '<pedidoId>'
done
```

Um serviço rodando fora do Docker (`dotnet run`) não exporta traces, porque o OTLP do Jaeger (4317) não é publicado no host.
