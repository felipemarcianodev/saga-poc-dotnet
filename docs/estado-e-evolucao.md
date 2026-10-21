# Estado atual e evolução

Levantamento feito em 2026-10-20 com a stack completa no Docker, em build Debug, com o log de todos os serviços em `logs/` e um monitor sobre as linhas de aviso e erro. Cada item abaixo foi reproduzido com requisições reais ou lido no código; o que não pôde ser confirmado está marcado como não verificado.

## Como foi feito

- **Casos de uso da SAGA** (`testar-casos-de-uso.sh`, 14 casos) e **fluxo de caixa** (`testar-fluxo-caixa.sh`, 4 cenários), repetidos a cada correção.
- **Entrada inválida** nas três APIs: corpo vazio, JSON quebrado, quantidade 0, preço negativo, forma de pagamento desconhecida, endereço curto, tipo de lançamento fora do enum, valores fora da faixa, datas extremas, período sem datas ou invertido.
- **Carga curta:** 15 pedidos ao mesmo tempo contra um pool de 10 entregadores.
- **Falhas reais:** serviço de pagamento parado no meio da SAGA, RabbitMQ reiniciado, orquestrador reiniciado com prazo pendente e orquestrador parado até a resposta de um serviço chegar antes do prazo.

## Defeitos encontrados e corrigidos

| Defeito | Evidência | Correção |
|---|---|---|
| Compensação não conferia o `Sucesso` das respostas, não tinha prazo e nunca enviava `LiberarEntregador` | Leitura do `PedidoSaga` | #11 |
| Handlers de compensação respondiam e relançavam a exceção: o Rebus descartava a resposta, e a SAGA esperava para sempre | Leitura dos handlers | #11 |
| `Dictionary` e `List` estáticos sem proteção nos serviços simulados, acessados por até 10 mensagens em paralelo | Leitura dos serviços | #11 |
| Cobrança depois do cancelamento: com o pagamento parado, o pedido expirou, e a cobrança que estava na fila foi aprovada ao religar o serviço, sem estorno | R$ 46,00 cobrados (`TXN_b486…`) de pedido cancelado | #13 |
| Prazo atrasado com a máquina carregada: a mensagem adiada não chegou antes de uma resposta de 40 s, e a SAGA concluiu o pedido em 52 s | Caso 14 terminou CONCLUÍDO numa rodada | #13 |
| Workers não mandavam log para o Seq: a configuração era montada e descartada | Leitura do `SerilogExtensions` | #12 |
| `Worker.cs` de template logava "Worker running at" a cada segundo nos quatro serviços da SAGA | Log em arquivo | #12 |
| Valores impressos como `¤46.00` (cultura invariante do container) e identificadores entre aspas no arquivo | Log em arquivo | #12 |
| `ASPNETCORE_URLS` duplicado (Dockerfile e compose) gerava aviso na subida das APIs | Log em arquivo | #12 |
| `POST /api/lancamentos` com `{}` gravava crédito de 150 para `COM001` (valores padrão do DTO) | Lançamento no banco | #15 |
| `tipo: 7` era gravado e somado como débito no consolidado | Consolidado com débito de 10 | #15 |
| Lançamento de 2 bilhões respondia 202 e era descartado em silêncio pelo worker | Aviso no log do worker | #15 |
| Pedido com quantidade 0, preço negativo, forma de pagamento desconhecida ou endereço curto respondia 202 e terminava cancelado ou compensado pela SAGA | Desfechos no log do orquestrador | #15 |
| Consulta de período sem datas, ou com início depois do fim, respondia 200 com lista vazia | Resposta da API | #15 |
| "Modo DEMO": sem barramento registrado, a API de pedidos respondia 202 sem enviar nada | Leitura do controller | #15 |

## Verificado e funcionando

- **Pool de entregadores esgotado:** de 15 pedidos simultâneos, 8 concluíram e 7 foram compensados com estorno ("Todos os entregadores estão ocupados no momento"); dois entregadores ainda estavam em entregas anteriores.
- **RabbitMQ reiniciado:** os serviços se reconectam sozinhos em 40 a 60 s (o Consolidado foi o último). O pedido enviado durante a queda ficou na fila e concluiu depois da reconexão. Nenhuma mensagem foi para a fila `error`.
- **Orquestrador reiniciado** 5 s depois de um pedido para `REST_LENTO`: o prazo, guardado em `rebus_timeouts`, disparou aos 30 s mesmo assim.
- **Fila `error`** vazia em todas as rodadas, inclusive com prazos chegando para SAGAs já concluídas.
- **`ConcurrencyException`** no log do orquestrador quando duas respostas chegam juntas para a mesma SAGA: o Rebus repete a mensagem e ela passa. É ruído esperado, não defeito.

## Pendências abertas

Em ordem de impacto. As que já estavam no backlog do `CLAUDE.md` estão indicadas.

1. **Status do pedido é mock** (backlog). `GET /api/pedidos/{id}/status` responde 200 com "EmProcessamento" para qualquer Guid, até `00000000-…`.
2. **Estado dos serviços em memória.** Transações, cancelamentos por pedido, idempotência e alocações se perdem quando o container reinicia: um reinício entre o cancelamento e uma operação atrasada deixa a operação passar.
3. **`COMPENSAÇÃO FALHOU` só fica no log** (backlog). A SAGA é removida do banco; não há fila nem tabela de intervenção manual.
4. **Pedido duplicado.** Cada `POST /api/pedidos` gera um `CorrelacaoId` novo; um cliente que repete a requisição (timeout de rede, clique duplo) cria dois pedidos e duas cobranças.
5. **Sem espera por entregador.** Com o pool esgotado, o pedido é compensado na hora, com estorno, em vez de tentar alocar de novo depois de alguns segundos.
6. **Idempotência do worker de lançamentos** (backlog). A repetição do comando grava o lançamento de novo.
7. **Datas sem faixa no lançamento.** `dataLancamento` de 1900 ou 2099 é aceita e entra no consolidado.
8. **`UseHttpsRedirection` em container sem HTTPS.** As três APIs avisam "Failed to determine the https port for redirect" a cada subida. Tirar ou condicionar o redirecionamento é decisão de segurança do dono.
9. **Traces da SAGA** (backlog). O orquestrador e os serviços da SAGA não aparecem no Jaeger.
10. **Testes de integração** (backlog). A SAGA e as APIs só são validadas pelos scripts de ponta a ponta.
11. **Datas com e sem fuso.** O consolidado devolve `"2026-10-20T00:00:00Z"`, o lançamento devolve `"2026-10-20T00:00:00"`.
12. **Não verificado: leitura no Seq.** A ingestão sem chave funciona (201), mas o login com `admin`/`admin123` do compose responde 401 com o volume já existente; não foi possível conferir os eventos dos workers pela interface.

## Caminho para um sistema completo

1. **Desfecho persistido:** o orquestrador publica as transições, e a API mantém uma projeção consultada pelo `/status`. A mesma tabela guarda os pedidos em `COMPENSAÇÃO FALHOU` para intervenção manual (pendências 1 e 3).
2. **Estado dos serviços fora da memória:** registro de cancelamentos, idempotência e transações no PostgreSQL ou no Redis (pendência 2).
3. **Idempotência de ponta a ponta:** cabeçalho `Idempotency-Key` na API de pedidos e deduplicação do comando no worker de lançamentos (pendências 4 e 6).
4. **Outbox:** gravar o estado e a mensagem de saída na mesma transação (o Rebus 8 oferece outbox; falta confirmar o suporte no `Rebus.PostgreSql` usado aqui), para o serviço não ficar com estado e mensageria divergentes se cair no meio.
5. **Qualidade:** testes de integração com Testcontainers, instrumentação do Rebus no Jaeger e um teste de carga que produza os números que hoje são só metas (pendências 9 e 10).
6. **Segurança:** autenticação nas APIs e decisão sobre HTTPS (pendência 8).
