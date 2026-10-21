# Casos de uso da SAGA

Este documento descreve os **14 casos de uso** da SAGA de pedidos: o que cada um força nos serviços simulados e qual desfecho o orquestrador deve registrar. Os payloads são os mesmos de `docs/scripts/testar-casos-de-uso.sh`, que envia os 14 casos e confere o desfecho de cada pedido.

Os serviços não têm banco nem integração externa: as falhas são disparadas por valores fixos no payload (identificador do restaurante, do cliente, do produto ou palavras no endereço).

---

## Resumo

| Nº | Caso | O que falha | Desfecho esperado | Compensação executada |
|---|---|---|---|---|
| 1 | Pedido Normal | nada | CONCLUIDO | nenhuma |
| 2 | Restaurante Fechado | restaurante (`REST_FECHADO`) | CANCELADO | nenhuma (nada foi feito ainda) |
| 3 | Item Indisponível | restaurante (produto `INDISPONIVEL`) | CANCELADO | nenhuma |
| 4 | Pagamento Recusado | pagamento (`CLI_CARTAO_RECUSADO`) | COMPENSADO | cancelamento no restaurante |
| 5 | Sem Entregador Disponível | entregador (endereço com `LONGE`) | COMPENSADO | estorno do pagamento e cancelamento no restaurante |
| 6 | Timeout no Pagamento | pagamento (`CLI_TIMEOUT`) | COMPENSADO | cancelamento no restaurante |
| 7 | Pedido Premium | nada | CONCLUIDO | nenhuma |
| 8 | Múltiplos Itens | nada | CONCLUIDO | nenhuma |
| 9 | Endereço Longe | nada | CONCLUIDO | nenhuma |
| 10 | Falha na Notificação | notificação (`CLI_SEM_NOTIFICACAO`) | CONCLUIDO | nenhuma: notificação não compensa |
| 11 | Pedido Agendado | nada | CONCLUIDO | nenhuma |
| 12 | Compensação Total | entregador (endereço com `LONGE` e `DISTANTE`) | COMPENSADO | estorno do pagamento e cancelamento no restaurante |
| 13 | Estorno Recusado | entregador (endereço com `LONGE`) e estorno (`CLI_ESTORNO_FALHA`) | COMPENSACAO_FALHOU | cancelamento no restaurante; o estorno é recusado nas 3 tentativas |
| 14 | Restaurante sem Resposta | restaurante demora 40 s (`REST_LENTO`) | EXPIRADO | cancelamento no restaurante pelo pedido (o prazo de 30 s expira antes da validação) |

Como o script classifica o desfecho, pelas linhas `[SAGA]` do orquestrador:

- **CONCLUIDO**: `Pedido {PedidoId} - CONCLUÍDO COM SUCESSO!`
- **CANCELADO**: `Pedido {PedidoId} - Cancelado: {Motivo}` (o restaurante recusou; não havia nada a desfazer)
- **COMPENSADO**: `Pedido {PedidoId} - COMPENSAÇÃO CONCLUÍDA ({Duracao}s)` (alguma etapa já tinha sido feita e foi desfeita)
- **COMPENSACAO_FALHOU**: `Pedido {PedidoId} - COMPENSAÇÃO FALHOU ...` (algum passo esgotou as tentativas ou o prazo; o log, em nível de erro, lista o que ficou sem compensação)
- **EXPIRADO**: `Pedido {PedidoId} - PRAZO EXPIRADO na etapa {Etapa}` seguido de `COMPENSAÇÃO CONCLUÍDA` (um serviço não respondeu a tempo)

A compensação de liberar o entregador (`LiberarEntregador`) só é enviada quando o prazo do pedido esgota durante a alocação; nenhum dos 14 casos força esse cenário. A única etapa depois da alocação é a notificação, e falha de notificação não compensa.

No caso 14, o prazo esgota com o restaurante ainda validando, e a SAGA envia `CancelarPedidoRestaurante` pelo `CorrelacaoId`. Quando a validação termina, 10 s depois, o restaurante vê o cancelamento e desfaz o pedido ("Pedido cancelado durante a validação"); a resposta, que chega com a SAGA já encerrada, é descartada pelo Rebus sem deixar nada para trás.

---

## Regras dos serviços simulados

**Restaurante** (`ServicoRestaurante`)
- `REST_FECHADO`: recusa com `RESTAURANTE_FECHADO`. `REST_INATIVO` e `REST_INVALIDO` também recusam; qualquer outro identificador que não comece com `REST` é "não encontrado".
- Produto com `produtoId` igual a `INDISPONIVEL`: recusa com `PRODUTO_INDISPONIVEL`. Quantidade acima de 10 ou preço menor ou igual a zero também recusam.
- Pedido abaixo de R$ 10,00: recusa com `VALOR_MINIMO`.
- Tempo de preparo: 15 minutos por item da lista (não por unidade); `REST_VIP` leva metade.
- `REST_LENTO`: responde depois de 40 s (`Simulacao:RestauranteLento:AtrasoSegundos`), mais que o prazo da SAGA.

**Orquestrador** (`PedidoSaga`)
- Taxa de entrega de 15% sobre o valor dos itens; o pagamento cobra itens + taxa.
- Prazo de 30 s para o pedido e de 30 s para a compensação; até 3 tentativas por compensação recusada, com 2 s entre elas (seção `Saga` do `appsettings.json`).

**Pagamento** (`ServicoPagamento`; a variante `ServicoPagamentoComPolly` existe, mas não está registrada no `Program.cs`)
- `CLI_CARTAO_RECUSADO`, `CLI_SALDO_INSUFICIENTE`, `CLI_TIMEOUT` e `CLI_ERRO_GATEWAY`: recusam, cada um com seu código.
- Valor acima de R$ 10.000,00 ou forma de pagamento fora de `CREDITO`, `DEBITO`, `PIX` e `DINHEIRO`: recusa.
- `CLI_ESTORNO_FALHA`: o pagamento é aprovado, mas o estorno é sempre recusado com `ESTORNO_RECUSADO`.

**Entregador** (`ServicoEntregador`)
- Endereço com `LONGE` ou `DISTANTE`: recusa com `AREA_NAO_COBERTA` ("Endereço fora da área de cobertura"). Endereço com menos de 10 caracteres também recusa.
- Horário de pico simulado: entre 12h e 14h59 UTC, 30% das alocações são recusadas com `HORARIO_PICO`. Desliga com `SIMULAR_HORARIO_PICO=false` no compose (o CI faz isso). Nesse horário, um caso que deveria terminar CONCLUIDO pode terminar COMPENSADO; o script marca isso como AVISO, não como falha.
- Pool de 10 entregadores (`ENT001` a `ENT010`). Cada um volta ao pool quando a entrega simulada termina (1 minuto estimado = 1 segundo). Com o pool esgotado, recusa com `SEM_ENTREGADOR_DISPONIVEL`.
- Distância sorteada entre 1 e 15 km; o tempo estimado é de cerca de 3 minutos por km mais uma base aleatória; `REST_VIP` tem tempo 30% menor.

**Notificação** (`ServicoNotificacao`)
- `CLI_SEM_NOTIFICACAO` e `CLI_INVALIDO`: falham. Fora isso, 5% dos envios falham ao acaso.
- O handler responde ao orquestrador mesmo quando o envio falha, e a SAGA conclui do mesmo jeito.

---

## Detalhamento

Todos os casos usam `POST /api/pedidos`. A API responde **202** com o `pedidoId` assim que envia o comando `IniciarPedido` para a `fila-orquestrador`; o 202 não diz nada sobre o desfecho.

```json
{
  "pedidoId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "mensagem": "Pedido recebido e está sendo processado. Use o PedidoId para consultar o status.",
  "status": "Pendente",
  "dataRecebimento": "2026-10-20T15:00:00Z"
}
```

### Caso 1: Pedido Normal (Happy Path)

Restaurante valida, pagamento aprova, entregador é alocado e o cliente é notificado.

```json
{
  "clienteId": "CLI001",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD001", "nome": "Pizza Margherita", "quantidade": 1, "precoUnitario": 45.90 }
  ],
  "enderecoEntrega": "Rua das Flores, 123 - Centro",
  "formaPagamento": "CREDITO"
}
```

Valor dos itens R$ 45,90, taxa de entrega R$ 6,89 (15%), preparo de 15 minutos.

Linhas `[SAGA]` do orquestrador (o identificador do pedido foi omitido):

```
[SAGA] Iniciando pedido ... - Cliente: CLI001
[SAGA] Pedido ... - Validação restaurante: APROVADO
[SAGA] Pedido ... - Pagamento: APROVADO
[SAGA] Pedido ... - Entregador: ALOCADO
[SAGA] Pedido ... - Entregador ENT001 alocado com sucesso!
[SAGA] Pedido ... - CONCLUÍDO COM SUCESSO!
[SAGA] Pedido ... - Tempo total: {segundos}s
[SAGA] Pedido ... - Tempo estimado entrega: {minutos} minutos
```

O entregador alocado e os tempos variam a cada execução (a distância é sorteada).

### Caso 2: Restaurante Fechado

O restaurante recusa; a SAGA termina sem chamar pagamento nem entregador e notifica o cliente.

```json
{
  "clienteId": "CLI001",
  "restauranteId": "REST_FECHADO",
  "itens": [
    { "produtoId": "PROD001", "nome": "Hambúrguer Artesanal", "quantidade": 2, "precoUnitario": 28.50 }
  ],
  "enderecoEntrega": "Av. Principal, 456",
  "formaPagamento": "DEBITO"
}
```

```
[SAGA] Pedido ... - Validação restaurante: REJEITADO
[SAGA] Pedido ... - Cancelado: [RESTAURANTE_FECHADO] Restaurante está fechado no momento
```

### Caso 3: Item Indisponível

Mesmo caminho do caso 2, com outro motivo.

```json
{
  "clienteId": "CLI002",
  "restauranteId": "REST002",
  "itens": [
    { "produtoId": "INDISPONIVEL", "nome": "Produto Esgotado", "quantidade": 1, "precoUnitario": 35.00 }
  ],
  "enderecoEntrega": "Rua das Palmeiras, 789",
  "formaPagamento": "PIX"
}
```

```
[SAGA] Pedido ... - Cancelado: [PRODUTO_INDISPONIVEL] Produto 'Produto Esgotado' está indisponível
```

### Caso 4: Pagamento Recusado

O restaurante aceita e o pagamento é recusado. A SAGA compensa a única etapa feita: cancela o pedido no restaurante.

```json
{
  "clienteId": "CLI_CARTAO_RECUSADO",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD003", "nome": "Sushi Combo", "quantidade": 1, "precoUnitario": 89.90 }
  ],
  "enderecoEntrega": "Rua das Acácias, 321",
  "formaPagamento": "CREDITO"
}
```

```
[SAGA] Pedido ... - Pagamento: REJEITADO
[SAGA] Pedido ... - COMPENSAÇÃO INICIADA
[SAGA] Pedido ... - Motivo: Cartão de crédito recusado pela operadora
[SAGA] Pedido ... - Pedido cancelado no restaurante
[SAGA] Pedido ... - COMPENSAÇÃO CONCLUÍDA ({segundos}s)
[SAGA] Pedido ... - Passos compensados: 1
```

### Caso 5: Sem Entregador Disponível

Restaurante e pagamento aprovam; o entregador recusa. A SAGA estorna o pagamento e cancela no restaurante.

```json
{
  "clienteId": "CLI003",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD004", "nome": "Salada Caesar", "quantidade": 3, "precoUnitario": 22.50 }
  ],
  "enderecoEntrega": "Rua MUITO LONGE do centro, 9999",
  "formaPagamento": "CREDITO"
}
```

O que força a recusa é a palavra `LONGE` no endereço, e o motivo registrado é "Endereço fora da área de cobertura" (`AREA_NAO_COBERTA`), não falta de entregador no pool. A falta real de entregador (pool esgotado ou horário de pico) leva ao mesmo desfecho.

```
[SAGA] Pedido ... - Entregador: INDISPONÍVEL
[SAGA] Pedido ... - COMPENSAÇÃO INICIADA
[SAGA] Pedido ... - Motivo: Endereço fora da área de cobertura
[SAGA] Pedido ... - Pagamento estornado com sucesso
[SAGA] Pedido ... - Pedido cancelado no restaurante
[SAGA] Pedido ... - COMPENSAÇÃO CONCLUÍDA ({segundos}s)
[SAGA] Pedido ... - Passos compensados: 2
```

O orquestrador envia o estorno e o cancelamento no mesmo handler, um depois do outro, mas eles são processados em paralelo por serviços diferentes; a ordem das duas confirmações no log pode variar.

### Caso 6: Timeout no Pagamento

```json
{
  "clienteId": "CLI_TIMEOUT",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD005", "nome": "Açaí 500ml", "quantidade": 1, "precoUnitario": 18.90 }
  ],
  "enderecoEntrega": "Rua dos Pinheiros, 555",
  "formaPagamento": "CREDITO"
}
```

Desfecho e compensação iguais aos do caso 4, com o motivo "Timeout ao comunicar com gateway de pagamento". O timeout é simulado: o serviço devolve um erro do tipo `Timeout` logo depois do atraso normal (200 a 800 ms). O handler tem um limite real de 10 segundos, que este caso não chega a atingir, e não há repetição com Polly, porque a variante com Polly não está registrada.

### Caso 7: Pedido Premium (VIP)

```json
{
  "clienteId": "CLI_VIP",
  "restauranteId": "REST_VIP",
  "itens": [
    { "produtoId": "PROD_PREMIUM", "nome": "Prato Executivo Premium", "quantidade": 1, "precoUnitario": 120.00 }
  ],
  "enderecoEntrega": "Av. Empresarial, 1000 - Sala 301",
  "formaPagamento": "CREDITO"
}
```

Caminho feliz. `REST_VIP` reduz o preparo à metade (7 minutos para 1 item, divisão inteira) e o tempo de entrega em 30%. O `clienteId` `CLI_VIP` não tem regra própria.

### Caso 8: Múltiplos Itens

```json
{
  "clienteId": "CLI004",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD001", "nome": "Pizza Margherita", "quantidade": 2, "precoUnitario": 45.90 },
    { "produtoId": "PROD002", "nome": "Refrigerante 2L", "quantidade": 1, "precoUnitario": 8.50 },
    { "produtoId": "PROD003", "nome": "Sorvete 1L", "quantidade": 1, "precoUnitario": 22.00 }
  ],
  "enderecoEntrega": "Rua das Margaridas, 222",
  "formaPagamento": "PIX"
}
```

Caminho feliz. Valor dos itens R$ 122,30; preparo de 45 minutos (3 itens na lista × 15 minutos).

### Caso 9: Endereço Longe

```json
{
  "clienteId": "CLI005",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD001", "nome": "Marmita Fitness", "quantidade": 1, "precoUnitario": 32.00 }
  ],
  "enderecoEntrega": "Bairro Afastado, Km 15",
  "formaPagamento": "CREDITO"
}
```

Caminho feliz. O serviço não calcula distância a partir do endereço (a distância é sorteada) e não tem taxa por distância; o endereço só não contém `LONGE` nem `DISTANTE`. O caso mostra que um endereço "afastado" sem essas palavras é atendido normalmente. Com `LONGE` ou `DISTANTE`, o desfecho seria o do caso 5.

### Caso 10: Falha na Notificação

```json
{
  "clienteId": "CLI_SEM_NOTIFICACAO",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD001", "nome": "Lanche Natural", "quantidade": 1, "precoUnitario": 15.50 }
  ],
  "enderecoEntrega": "Rua das Oliveiras, 444",
  "formaPagamento": "DEBITO"
}
```

Restaurante, pagamento e entregador aprovam; a notificação falha com `NOTIFICACOES_DESATIVADAS`. O serviço de notificação registra `Falha ao enviar notificação (não crítico)` e responde ao orquestrador do mesmo jeito; a SAGA termina com `CONCLUÍDO COM SUCESSO!`. Notificação é tratada como melhor esforço: não há compensação nem nova tentativa.

### Caso 11: Pedido Agendado

```json
{
  "clienteId": "CLI006",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD007", "nome": "Bolo de Aniversário", "quantidade": 1, "precoUnitario": 85.00 }
  ],
  "enderecoEntrega": "Rua das Festas, 123 - Apto 501",
  "formaPagamento": "PIX"
}
```

Caminho feliz. **O agendamento não existe no código:** `CriarPedidoRequest` não tem campo de data agendada, e um `dataAgendamento` enviado no JSON é ignorado. O caso existe para marcar o lugar da funcionalidade; implementá-la exigiria um agendador (Quartz.NET, Hangfire ou mensagem com entrega adiada no Rebus).

### Caso 12: Compensação Total

```json
{
  "clienteId": "CLI007",
  "restauranteId": "REST001",
  "itens": [
    { "produtoId": "PROD008", "nome": "Combo Família", "quantidade": 1, "precoUnitario": 150.00 }
  ],
  "enderecoEntrega": "Endereço MUITO LONGE E DISTANTE",
  "formaPagamento": "CREDITO"
}
```

Mesmo mecanismo do caso 5 (recusa do entregador por `AREA_NAO_COBERTA` depois do pagamento aprovado), com valor maior: R$ 150,00 de itens mais R$ 22,50 de taxa, estornados. Compensações: estorno do pagamento e cancelamento no restaurante (`Passos compensados: 2`). "Total" quer dizer todas as etapas concluídas até a falha; a liberação do entregador não entra, porque ele não chegou a ser alocado.

---

## Como testar

### Script (recomendado)

```bash
bash docs/scripts/testar-casos-de-uso.sh        # os 14 casos
bash docs/scripts/testar-casos-de-uso.sh 4      # um caso
```

O script envia os payloads, espera os desfechos no log do orquestrador (até 90 s), confere a fila `error` e sai com 1 se algum caso divergir do esperado. Veja `docs/scripts/readme-script.md`.

### Swagger ou curl

Swagger na raiz de `http://localhost:5000` (`POST /api/pedidos`, "Try it out").

No Git Bash do Windows, passar JSON com acento direto no argumento do `curl` corrompe o UTF-8; grave o payload num arquivo e envie com `--data-binary`:

```bash
curl -X POST http://localhost:5000/api/pedidos \
  -H "Content-Type: application/json; charset=utf-8" \
  --data-binary @pedido.json
```

### Conferir o desfecho

O desfecho está no log do orquestrador, não na resposta da API:

```bash
docker logs saga-orquestrador 2>&1 | grep '\[SAGA\]' | grep <pedidoId>
```

`GET /api/pedidos/{id}/status` ainda é um **mock**: responde sempre `EmProcessamento`. A SAGA concluída (com sucesso ou compensada) é removida do banco pelo Rebus (`MarkAsComplete()`), e o desfecho final não fica persistido em lugar nenhum; uma projeção de status está no backlog.

Mensagem na fila `error` do RabbitMQ (http://localhost:15672) indica bug: nenhum dos 14 casos deveria levar mensagem para lá.

---

## Rastreando um pedido

O `pedidoId` devolvido pela API é o `CorrelacaoId` de todas as mensagens da SAGA e aparece nos logs de cada serviço.

- **Logs:** `docker logs <container>` ou o Seq (http://localhost:5342), filtrando por `CorrelacaoId`.
- **Traces:** o Jaeger (http://localhost:16686) mostra hoje só a API de pedidos; o orquestrador e os serviços da SAGA não geram span (o Rebus não tem instrumentação configurada). Ver o backlog no `CLAUDE.md`.

---

## Limitações

- Os serviços são simulações em memória: o estado de transações, cancelamentos e entregadores se perde quando o container reinicia.
- Não há teste automatizado da SAGA além do script de ponta a ponta; os testes xUnit da solution cobrem só o domínio do fluxo de caixa.
- Não há métricas de negócio (taxa de sucesso, taxa de compensação, duração da SAGA) coletadas; o tempo total aparece só no log `[SAGA]` de cada pedido.

---

## Referências

- [arquitetura.md](./arquitetura.md): visão macro dos serviços
- [diagramas-compensacao.md](./diagramas-compensacao.md): diagramas dos fluxos de compensação
