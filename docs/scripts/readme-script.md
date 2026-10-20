# Scripts de teste

Três scripts Bash validam a stack de ponta a ponta. Todos conferem o resultado real, e não só o código HTTP, e saem com código 1 quando algo diverge. São os mesmos que o CI (`.github/workflows/ci.yml`) roda em todo PR.

| Script | O que valida |
|---|---|
| `aguardar-stack.sh` | APIs, filas e assinaturas prontas logo depois do `up` |
| `testar-casos-de-uso.sh` | Os 14 casos da SAGA, pelo desfecho no log do orquestrador, e a fila `error` |
| `testar-fluxo-caixa.sh` | 4 cenários do fluxo de caixa, do lançamento ao consolidado |

Rodam no Linux, no macOS e no Git Bash do Windows. Precisam só de `bash`, `curl` e `docker`.

---

## Pré-requisitos

A partir da raiz do repositório:

```bash
docker compose -f docker/docker-compose.yml up -d --build
bash docs/scripts/aguardar-stack.sh
```

---

## `aguardar-stack.sh`

Espera a stack ficar pronta depois de um `docker compose up`. Container no ar não basta: mensagem enviada antes de o orquestrador declarar a fila dele, ou antes de o Consolidado assinar os eventos, é descartada pelo RabbitMQ sem erro. O script só libera quando as três APIs respondem 200 em `/health`, as sete filas existem e as duas assinaturas do Consolidado estão no exchange `RebusTopics`.

```bash
bash docs/scripts/aguardar-stack.sh
TEMPO_MAXIMO=300 bash docs/scripts/aguardar-stack.sh   # padrão: 180 s
```

Sai com código 1 e lista o que ficou pendente se o tempo acabar.

---

## `testar-casos-de-uso.sh`

Testa os 14 casos de uso da SAGA (delivery de comida).

A API responde 202 assim que aceita o pedido, o que não prova que a SAGA terminou. Por isso o script lê os logs do orquestrador (`docker logs saga-orquestrador`) e compara o desfecho de cada pedido com o esperado. No fim, espera até 45 s desde o envio (`ESPERA_ATRASADAS`), para cobrir as mensagens que chegam depois do desfecho (prazos e a resposta do restaurante lento), e confere se a fila `error` recebeu mensagens novas.

```bash
bash docs/scripts/testar-casos-de-uso.sh       # todos os casos
bash docs/scripts/testar-casos-de-uso.sh 5     # um caso (1-14)

# Outra URL de API ou outro tempo máximo de espera
BASE_URL=http://localhost:8080 TEMPO_MAXIMO=90 bash docs/scripts/testar-casos-de-uso.sh
```

| Caso | Cenário | Desfecho esperado |
|---|---|---|
| 1 | Pedido normal (happy path) | Concluído |
| 2 | Restaurante fechado | Cancelado |
| 3 | Item indisponível | Cancelado |
| 4 | Pagamento recusado | Compensado |
| 5 | Sem entregador disponível | Compensado |
| 6 | Timeout no pagamento | Compensado |
| 7 | Pedido premium (VIP) | Concluído |
| 8 | Múltiplos itens | Concluído |
| 9 | Endereço longe | Concluído |
| 10 | Falha na notificação | Concluído |
| 11 | Pedido agendado | Concluído |
| 12 | Compensação total | Compensado |
| 13 | Estorno recusado | Compensação falhou |
| 14 | Restaurante sem resposta | Expirado |

- **Cancelado:** a SAGA para antes de qualquer passo que precise ser desfeito.
- **Compensado:** um passo falhou depois de outros terem sido executados, e a SAGA desfez os anteriores (estorno, liberação do entregador, cancelamento no restaurante).
- **Compensação falhou:** alguma compensação foi recusada em todas as tentativas ou não respondeu no prazo; o log de erro lista o que ficou pendente.
- **Expirado:** um serviço não respondeu no prazo do pedido (30 s), e a SAGA compensou o que já tinha feito.

> **Horário de pico simulado:** entre 12h e 14h59 (UTC), o serviço de entregador recusa 30% das alocações. Nessa janela, um caso que deveria concluir pode terminar compensado; o script marca isso como **AVISO**, e não como falha. Para desligar a simulação, suba a stack com `SIMULAR_HORARIO_PICO=false` (é o que o CI faz).

---

## `testar-fluxo-caixa.sh`

Testa o fluxo de caixa (CQRS) de ponta a ponta: lançamentos pela API de escrita (porta 5200), gravação e publicação pelo worker, aplicação dos eventos pelo Consolidado e leitura pela API de consulta (porta 5100).

O 202 da API de lançamentos não prova nada. O script espera as filas `fluxocaixa-lancamentos` e `fluxocaixa-consolidado` esvaziarem e só então confere os lançamentos gravados e os valores do consolidado. Cada cenário usa um comerciante novo, então o cache da API de consulta não interfere entre rodadas.

```bash
bash docs/scripts/testar-fluxo-caixa.sh

# Outras URLs ou tempo de espera
LANCAMENTOS_URL=http://localhost:5200 CONSULTA_URL=http://localhost:5100 TEMPO_MAXIMO=90 bash docs/scripts/testar-fluxo-caixa.sh
```

| Cenário | O que envia | O que confere |
|---|---|---|
| 1 | Crédito de 150,50 e débito de 40,25 no mesmo dia | 2 lançamentos confirmados; saldo 110,25 |
| 2 | 5 créditos (100 a 500) e 3 débitos (50, 75, 125) | 8 lançamentos; créditos 1500, débitos 250, saldo 1250 |
| 3 | Crédito de 1000 ontem e débito de 250 hoje | Dois consolidados separados: saldo 1000 ontem e -250 hoje |
| 4 | Valor zero, descrição e comerciante curtos demais | HTTP 400 e nenhum lançamento gravado |

Ao final, confere que nenhuma mensagem nova caiu na fila `error`. Sai com código 1 se qualquer verificação falhar.

---

## Quando um script falha

| Sintoma | Onde olhar |
|---|---|
| `aguardar-stack.sh` estoura o tempo | `docker compose -f docker/docker-compose.yml ps -a` e `docker logs <container>` do item pendente |
| Caso da SAGA com desfecho diferente | `docker logs saga-orquestrador` (linhas `[SAGA]`) e o log do serviço da etapa que falhou |
| Saldo do consolidado divergente | `docker logs lancamentos-worker` e `docker logs fluxocaixa-consolidado` |
| Mensagem na fila `error` | É sempre bug: abra a mensagem no RabbitMQ (http://localhost:15672, `saga` / `saga123`) e leia o cabeçalho `rbs2-error-details` |
| JSON com acento corrompido no Git Bash | Envie o payload como arquivo (`--data-binary @arquivo`), como os scripts fazem |

Diagnóstico mais detalhado: [runbook da SAGA](../runbook-troubleshooting.md) e [troubleshooting do fluxo de caixa](../troubleshooting-fluxo-caixa.md).
