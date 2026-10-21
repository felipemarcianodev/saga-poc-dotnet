# Fluxo de caixa

Segundo contexto da POC: controle de lançamentos (débitos e créditos) de um comerciante e consolidado diário do saldo, com CQRS. A escrita registra lançamentos e publica eventos; a leitura mantém o consolidado do dia num banco separado, com cache.

- Diagramas: [diagramas-fluxo-caixa.md](./diagramas-fluxo-caixa.md)
- Decisões: [ADR 001 (CQRS)](./decisoes-tecnicas/001-cqrs.md), [ADR 002 (cache)](./decisoes-tecnicas/002-cache.md), [ADR 003 (PostgreSQL)](./decisoes-tecnicas/003-postgresql.md)
- Diagnóstico: [troubleshooting-fluxo-caixa.md](./troubleshooting-fluxo-caixa.md)

---

## Componentes

| Serviço (compose) | Projeto | Papel |
|---|---|---|
| `lancamentos-api` (porta 5200) | `src/SagaPoc.ServicoLancamentos/SagaPoc.Lancamentos.Api` | Recebe `POST /api/lancamentos`, valida e envia o comando `RegistrarLancamento` para a fila `fluxocaixa-lancamentos`; responde 202. Também lista e consulta lançamentos gravados. |
| `lancamentos-worker` | `src/SagaPoc.ServicoLancamentos/SagaPoc.Lancamentos.Worker` | Consome o comando, grava o lançamento em `fluxocaixa_lancamentos` e publica `LancamentoCreditoRegistrado` ou `LancamentoDebitoRegistrado`. Migra esse banco ao subir. |
| `fluxocaixa-consolidado` | `src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado` | Assina os dois eventos (fila `fluxocaixa-consolidado`), cria ou atualiza o consolidado do dia em `fluxocaixa_consolidado` e apaga a chave do dia no Redis. Migra esse banco ao subir. |
| `fluxocaixa-api` (porta 5100) | `src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api` | Consulta o consolidado, com cache em memória, Redis e response cache. |

Bibliotecas compartilhadas, em `src/SagaPoc.ServicoFluxoCaixa/`:

- `SagaPoc.FluxoCaixa.Domain`: agregados `Lancamento` e `ConsolidadoDiario`, comando `RegistrarLancamento`, eventos, interfaces de repositório e `IUnitOfWork`.
- `SagaPoc.FluxoCaixa.Application`: serviços de aplicação (`LancamentoAppService`, `ConsolidadoAppService`) e `ICacheService`.
- `SagaPoc.FluxoCaixa.Infrastructure`: `DbContext`s do EF Core (Npgsql), repositórios, `UnitOfWork` e `RedisCacheService`.

Mensageria com Rebus sobre RabbitMQ. O comando vai por `Send` (roteado para `fluxocaixa-lancamentos`); os eventos vão por `Publish`, que no RabbitMQ passa pela exchange de tópicos `RebusTopics`. O Consolidado assina os dois eventos ao subir; sem a assinatura, o RabbitMQ descarta os eventos em silêncio.

---

## Domínio

**`Lancamento`** (agregado raiz)
- Tipo: `1` = débito (saída), `2` = crédito (entrada).
- Regras de `Lancamento.Criar`: valor maior que zero e até 1 bilhão; descrição obrigatória, até 500 caracteres; comerciante obrigatório.
- Estados: `Pendente`, `Confirmado`, `Cancelado`. O worker cria e confirma em memória, e o lançamento entra no banco já `Confirmado`.
- Eventos: `LancamentoCreditoRegistrado` ou `LancamentoDebitoRegistrado`, conforme o tipo. `Cancelar` e o evento `LancamentoCancelado` existem no domínio, mas nenhum endpoint ou handler os usa.

**`ConsolidadoDiario`**
- Uma linha por dia e comerciante (índice único em `Data` + `Comerciante`).
- Guarda total e quantidade de créditos e de débitos; `SaldoDiario` = créditos − débitos.

---

## Como rodar

O fluxo de caixa sobe junto com a stack inteira:

```bash
docker compose -f docker/docker-compose.yml up -d --build
bash docs/scripts/aguardar-stack.sh      # espera APIs, filas e assinaturas
bash docs/scripts/testar-fluxo-caixa.sh  # teste de ponta a ponta
```

Os workers aplicam as migrations ao subir; não é preciso rodar `dotnet ef`.

O compose sobe as duas APIs com `ASPNETCORE_ENVIRONMENT=Development`, então o Swagger fica em `http://localhost:5200/swagger` (lançamentos) e `http://localhost:5100/swagger` (consulta).

---

## API de lançamentos (porta 5200)

### `POST /api/lancamentos`

```json
{
  "tipo": 2,
  "valor": 150.00,
  "dataLancamento": "2026-10-20",
  "descricao": "Venda de produto X",
  "comerciante": "COM001",
  "categoria": "Vendas"
}
```

Validação (DataAnnotations, responde 400): `tipo` obrigatório, `1` ou `2`; `valor` obrigatório, de 0,01 a 1 bilhão; descrição de 3 a 500 caracteres; comerciante de 3 a 100 caracteres; categoria até 100 caracteres, opcional. Sem `dataLancamento`, vale a data atual. O corpo não tem valores padrão: até 2026-10-20, um `POST {}` gravava um crédito de 150 para `COM001`, e um `tipo` fora do enum era gravado e contado como débito.

Resposta 202:

```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mensagem": "Lançamento enviado para processamento"
}
```

O 202 só diz que o comando foi enviado. O `correlationId` **não** é o `id` do lançamento: o worker gera o `id` ao gravar, e a API não devolve esse valor. Para achar o lançamento, use a listagem por comerciante.

### `GET /api/lancamentos?comerciante={comerciante}&inicio={data}&fim={data}`

Lista os lançamentos do período (padrão: últimos 30 dias até hoje). `comerciante` é obrigatório; início depois do fim responde 400.

```json
[
  {
    "id": "8b0d9c1e-2f3a-4b5c-9d6e-7f8a9b0c1d2e",
    "tipo": "Credito",
    "valor": 150.00,
    "dataLancamento": "2026-10-20T00:00:00",
    "descricao": "Venda de produto X",
    "comerciante": "COM001",
    "categoria": "Vendas",
    "status": "Confirmado",
    "criadoEm": "2026-10-20T14:30:00Z"
  }
]
```

### `GET /api/lancamentos/{id}`

Consulta um lançamento pelo `id` gravado; 404 se não existir.

---

## API de consulta (porta 5100)

### `GET /api/consolidado/{comerciante}/{data}`

```json
{
  "data": "2026-10-20T00:00:00Z",
  "comerciante": "COM001",
  "totalCreditos": 1500.00,
  "totalDebitos": 250.00,
  "saldoDiario": 1250.00,
  "quantidadeCreditos": 5,
  "quantidadeDebitos": 3,
  "quantidadeTotalLancamentos": 8,
  "ultimaAtualizacao": "2026-10-20T14:30:00Z"
}
```

404 se o dia ainda não tem lançamento consolidado.

### `GET /api/consolidado/{comerciante}/periodo?inicio={data}&fim={data}`

Lista os consolidados do período. `inicio` e `fim` são obrigatórios, e início depois do fim responde 400 (antes, sem as datas, a rota respondia 200 com lista vazia). Esta rota não usa cache.

### Cache e defasagem

A consulta por dia passa por três camadas: response cache (60 s), memória (1 min) e Redis (5 min), nessa ordem, antes do banco. O Consolidado apaga só a chave do Redis a cada evento; a memória e o response cache não são invalidados. **Uma leitura pode vir defasada até cerca de 60 segundos** depois de um lançamento.

No Redis fica um snapshot do consolidado (`ConsolidadoDiarioCache`), remontado como agregado na leitura; o agregado em si não é desserializável. Detalhes no [ADR 002](./decisoes-tecnicas/002-cache.md).

As rotas da API de consulta têm um limitador de janela fixa: 50 requisições por segundo, com fila de 10. É uma configuração, não uma vazão medida: não há teste de carga.

---

## Testes

- **Unidade:** `tests/SagaPoc.FluxoCaixa.Domain.Tests`, 19 testes xUnit: 16 do agregado `Lancamento` (criação, validações, confirmação, cancelamento e eventos) e 3 do snapshot do consolidado no cache (reconstituição e ida e volta pelo JSON). As regras do `ConsolidadoDiario`, os handlers e as APIs não têm teste de unidade.
- **Ponta a ponta:** `docs/scripts/testar-fluxo-caixa.sh`, com a stack no ar. Cada cenário usa um comerciante novo, para não ler valor do cache de uma rodada anterior:
  1. crédito e débito no mesmo dia: confere saldo, totais e quantidades;
  2. cinco créditos e três débitos no mesmo dia acumulam no mesmo consolidado;
  3. lançamentos em dias diferentes ficam em consolidados separados (ontem e hoje);
  4. a API recusa entrada inválida com 400 (valor zero, descrição e comerciante curtos) e nada é gravado.

  Depois de enviar, o script espera as filas `fluxocaixa-lancamentos` e `fluxocaixa-consolidado` esvaziarem e confere que nenhuma mensagem nova foi para a fila `error`. Sai com 1 se algo divergir.

Os dois rodam no CI (`.github/workflows/ci.yml`).

---

## Dados

Banco `fluxocaixa_lancamentos` (porta 5433), tabela `lancamentos`: `id`, `tipo` e `status` gravados como texto, `valor`, `data_lancamento`, `descricao`, `comerciante`, `categoria`, `criado_em`, `atualizado_em`; índices em (`comerciante`, `data_lancamento`), `data_lancamento` e `status`.

Banco `fluxocaixa_consolidado` (porta 5434), tabela `ConsolidadosDiarios`, com índice único em (`Data`, `Comerciante`).

O esquema de cada banco é definido pelas migrations do EF Core em `SagaPoc.FluxoCaixa.Infrastructure`. Consultas SQL de operação estão em [docker/README.md](../docker/README.md).

---

## Observabilidade

- **Logs:** Serilog no console de cada container (`docker logs`). O Seq (http://localhost:5342) recebe os logs das APIs; os workers só escrevem no console (ver backlog no `CLAUDE.md`).
- **Traces:** a API de lançamentos e o worker exportam por OTLP para o Jaeger (http://localhost:16686). O Consolidado não configura OpenTelemetry, e a API de consulta não recebe o endereço do Jaeger no compose.
- **Health:** `/health` nas duas APIs.

Não há métricas (Prometheus, Grafana) na stack.

---

## Limitações conhecidas

- **Sem idempotência no worker de lançamentos:** cada tentativa de `RegistrarLancamento` gera um `Id` novo. Se o envio do evento falhar depois do commit, o Rebus repete a mensagem e o lançamento é gravado de novo.
- **Sem idempotência no Consolidado:** um evento reentregue soma o valor de novo.
- **Sem outbox:** a gravação do lançamento e a publicação do evento não são atômicas.
- **Consolidado processa uma mensagem por vez** (`MaxParallelism = 1`), porque a linha do dia não tem controle de concorrência; dois eventos em paralelo perderiam uma atualização. Escalar exige concorrência otimista ou UPDATE atômico.
- **Recusa silenciosa no worker:** se `Lancamento.Criar` recusar o comando, o worker registra um aviso e descarta a mensagem; o cliente já recebeu 202. A validação da API cobre as mesmas regras (tipo, faixa de valor, descrição e comerciante), então isso só acontece com comando enviado direto para a fila.
- **Leitura defasada** até cerca de 60 s, como descrito acima.