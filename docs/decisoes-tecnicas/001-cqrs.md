# ADR 001: Separação de escrita e leitura no fluxo de caixa (CQRS)

## Status

**Aceito** (implementado)

## Contexto

O registro de lançamentos precisava continuar funcionando mesmo se o consolidado diário caísse ou ficasse lento.

Num serviço único com um só banco:
- se o relatório trava, o registro de lançamentos trava junto;
- a consulta de agregação disputa recursos com os INSERTs;
- um deploy com defeito derruba as duas funções.

### Opções consideradas

1. **Serviço único com banco único:** simples, mas um problema afeta tudo.
2. **Serviços separados com banco compartilhado:** isola o processo, mas o banco continua sendo ponto comum de falha e de contenção.
3. **CQRS com bancos separados:** mais peças, mas a escrita deixa de depender da leitura.

## Decisão

Escrita e leitura em serviços e bancos diferentes, ligados por eventos no RabbitMQ (Rebus):

```
lancamentos-api ──Send RegistrarLancamento──> lancamentos-worker ──> PostgreSQL fluxocaixa_lancamentos
                                                     │
                                       Publish LancamentoCreditoRegistrado /
                                               LancamentoDebitoRegistrado
                                                     │
                                                     v
                         fluxocaixa-consolidado ──> PostgreSQL fluxocaixa_consolidado (+ apaga a chave no Redis)
                                                     ^
                         fluxocaixa-api (consulta) ──┘ (lê via memória, Redis e response cache)
```

- **Escrita:** a API de lançamentos (porta 5200) só valida e envia o comando; o worker grava e publica o evento.
- **Leitura:** o Consolidado assina os dois eventos e mantém uma linha por dia e comerciante; a API de consulta (porta 5100) lê essa tabela.

Detalhes em [fluxo-caixa.md](../fluxo-caixa.md).

## Consequências

### O que ganhamos

1. **Disponibilidade separada:** o Consolidado pode cair sem afetar o registro; os eventos esperam na fila `fluxocaixa-consolidado` até ele voltar. Isso decorre do desenho; não foi testado com falha injetada.
2. **Leitura barata:** a consulta do dia lê uma linha pré-calculada, sem SUM sobre os lançamentos.
3. **Escala separada, em tese:** a API de consulta é sem estado e pode ter réplicas. O Consolidado não pode, hoje (ver abaixo).

### O que perdemos

1. **Consistência imediata:** o lançamento leva um tempo para aparecer no consolidado (filas e worker), e o cache da API de consulta soma até cerca de 60 s de defasagem ([ADR 002](./002-cache.md)).
2. **Complexidade:** dois bancos, broker, assinatura de tópicos e dois workers.
3. **Depuração mais difícil:** o fluxo é assíncrono, e só a API de lançamentos e o worker exportam traces; o Consolidado não.

## O que pode dar errado

- **Lançamento duplicado:** não há outbox, e a gravação e a publicação não são atômicas. Se a publicação falhar depois do commit, o Rebus repete a mensagem e o worker grava outro lançamento, com `Id` novo. A correção prevista é usar o `CorrelationId` do comando como chave de deduplicação.
- **Evento aplicado duas vezes:** o Consolidado não guarda quais eventos já aplicou; uma reentrega soma o valor de novo.
- **Atualização perdida no consolidado:** a linha do dia não tem controle de concorrência (ler, alterar e gravar). Por isso o Consolidado processa uma mensagem por vez (`MaxParallelism = 1`) e não pode ter réplicas. Escalar exige concorrência otimista ou UPDATE atômico.
- **Evento descartado:** se o Consolidado ainda não assinou os tópicos, o RabbitMQ descarta o evento em silêncio. A assinatura acontece na subida do Consolidado.
- **Modelos divergentes sem detecção:** não há job de reconciliação que compare os lançamentos com o consolidado.
- **Recusa silenciosa:** se o domínio recusar o comando no worker, a mensagem é descartada com um aviso no log, depois de a API já ter respondido 202.
- **Carga:** não há teste de carga nem de volume; o comportamento com muitos eventos por segundo não é conhecido.

## O que já existe para conter isso

- Repetição de mensagem pelo Rebus (padrão: 5 tentativas) e fila `error` para o que esgota as tentativas.
- `ExecutarEmTransacaoAsync`, que roda a gravação na estratégia de repetição do EF Core (`EnableRetryOnFailure`).
- `docs/scripts/testar-fluxo-caixa.sh`, que confere de ponta a ponta os totais do consolidado e a fila `error` em todo PR.

## O que não foi feito

- Outbox para gravar o evento junto com o lançamento.
- Idempotência no worker e no Consolidado.
- Job de reconciliação.
- Teste de carga.

## Referências

- [CQRS, Martin Fowler](https://martinfowler.com/bliki/CQRS.html)
- [What do you mean by "Event-Driven"?, Martin Fowler](https://martinfowler.com/articles/201701-event-driven.html)

---

**Data:** 2026-01-15 (revisado em 2026-10-20)
