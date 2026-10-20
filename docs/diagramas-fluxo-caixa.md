# Diagramas do fluxo de caixa

Diagramas de componentes e de sequência do contexto de Fluxo de Caixa (CQRS), como está no código e no `docker/docker-compose.yml`. O contexto está descrito em [fluxo-caixa.md](./fluxo-caixa.md).

---

## 1. Componentes

```mermaid
graph TB
    COMERCIANTE[Cliente HTTP<br/>comerciante]

    subgraph Escrita["Escrita"]
        LAPI[lancamentos-api<br/>porta 5200<br/>POST/GET /api/lancamentos]
        WORKER[lancamentos-worker<br/>grava e publica eventos]
        DBL[(PostgreSQL<br/>fluxocaixa_lancamentos<br/>porta 5433)]
    end

    subgraph Mensageria["RabbitMQ"]
        QL[[fila fluxocaixa-lancamentos]]
        TOP{{exchange RebusTopics}}
        QC[[fila fluxocaixa-consolidado]]
    end

    subgraph Leitura["Leitura"]
        CONS[fluxocaixa-consolidado<br/>worker, MaxParallelism = 1]
        DBC[(PostgreSQL<br/>fluxocaixa_consolidado<br/>porta 5434)]
        REDIS[(Redis<br/>porta 6379)]
        CAPI[fluxocaixa-api<br/>porta 5100<br/>GET /api/consolidado]
    end

    COMERCIANTE -->|POST lançamento| LAPI
    LAPI -->|Send RegistrarLancamento| QL
    QL --> WORKER
    WORKER -->|INSERT| DBL
    WORKER -->|Publish evento| TOP
    TOP -->|assinatura| QC
    QC --> CONS
    CONS -->|cria ou atualiza o dia| DBC
    CONS -->|apaga a chave do dia| REDIS

    COMERCIANTE -->|GET consolidado| CAPI
    CAPI --> REDIS
    CAPI --> DBC
    LAPI -->|GET lançamentos| DBL
```

- A API de lançamentos não grava: valida o corpo (DataAnnotations), envia o comando e responde 202. O `GET /api/lancamentos` lê direto do banco de lançamentos.
- O worker de lançamentos publica `LancamentoCreditoRegistrado` ou `LancamentoDebitoRegistrado` (`Publish`). O Consolidado assina os dois ao subir; sem a assinatura, o RabbitMQ descartaria os eventos em silêncio.
- Cada worker migra o próprio banco ao subir; as APIs não migram.

---

## 2. Registrar um lançamento

```mermaid
sequenceDiagram
    participant C as Comerciante
    participant API as lancamentos-api
    participant MQ as RabbitMQ
    participant W as lancamentos-worker
    participant DBL as PostgreSQL<br/>lançamentos
    participant CO as Consolidado
    participant DBC as PostgreSQL<br/>consolidado
    participant R as Redis

    C->>API: POST /api/lancamentos {tipo, valor, dataLancamento, descricao, comerciante}
    API->>API: DataAnnotations (400 se inválido)
    API->>MQ: Send RegistrarLancamento
    API-->>C: 202 {correlationId}

    MQ->>W: RegistrarLancamento
    W->>W: Lancamento.Criar + Confirmar
    W->>DBL: INSERT (ExecutarEmTransacaoAsync)
    W->>MQ: Publish LancamentoCreditoRegistrado / LancamentoDebitoRegistrado
    Note over W,MQ: o Rebus só envia depois que o handler termina sem erro

    MQ->>CO: evento (fila fluxocaixa-consolidado)
    CO->>DBC: SELECT consolidado do dia e comerciante
    alt não existe
        CO->>DBC: INSERT com o primeiro valor
    else existe
        CO->>DBC: UPDATE totais e quantidades
    end
    CO->>R: DEL consolidado:{comerciante}:{data}
```

Se o domínio recusar o lançamento no worker (`Lancamento.Criar` falha), o handler registra um aviso e descarta a mensagem: o cliente já recebeu 202 e não fica sabendo. A validação da API cobre quase as mesmas regras, então isso só acontece com comando enviado direto para a fila.

---

## 3. Consultar o consolidado

```mermaid
sequenceDiagram
    participant C as Cliente
    participant RC as Response cache<br/>(60 s)
    participant API as fluxocaixa-api
    participant M as Memória<br/>(1 min)
    participant R as Redis<br/>(5 min)
    participant DB as PostgreSQL<br/>consolidado

    C->>RC: GET /api/consolidado/{comerciante}/{data}
    alt resposta guardada
        RC-->>C: 200 (sem chegar ao controller)
    else
        RC->>API: requisição
        API->>M: chave consolidado:{comerciante}:{data}
        alt achou na memória
            M-->>API: consolidado
        else
            API->>R: mesma chave
            alt achou no Redis
                R-->>API: consolidado (e grava na memória)
            else
                API->>DB: SELECT
                DB-->>API: consolidado ou 404
                API->>R: grava (5 min)
                API->>M: grava (1 min)
            end
        end
        API-->>C: 200
    end
```

O Consolidado só apaga a chave no Redis. A memória da API e o response cache não são invalidados, então uma leitura pode vir defasada até cerca de 60 segundos depois de um lançamento.

O ramo "achou no Redis" não acontece hoje: a API grava o agregado no Redis, mas falha ao desserializá-lo, registra um aviso e segue para o banco ([ADR 002](./decisoes-tecnicas/002-cache.md)). O `GET /api/consolidado/{comerciante}/periodo` não usa cache: vai direto ao banco.

As rotas da API de consulta passam por um limitador de janela fixa (`consolidado`: 50 requisições por segundo, fila de 10). Não há teste de carga que meça a vazão real.

---

## 4. Estados de um lançamento

```mermaid
stateDiagram-v2
    [*] --> Pendente: Lancamento.Criar
    Pendente --> Confirmado: Confirmar (ainda em memória, no worker)
    Confirmado --> [*]: INSERT + evento publicado
    Pendente --> Cancelado: Cancelar
    Confirmado --> Cancelado: Cancelar
```

O worker cria e confirma o lançamento antes do INSERT, então ele entra no banco já `Confirmado`. O domínio tem `Cancelar` e o evento `LancamentoCancelado`, mas nenhum endpoint nem handler os usa, e o Consolidado não trata cancelamento.

---

## 5. O que acontece em falha

```mermaid
sequenceDiagram
    participant MQ as RabbitMQ
    participant W as lancamentos-worker
    participant DB as PostgreSQL

    MQ->>W: RegistrarLancamento
    W->>DB: INSERT
    DB-->>W: erro transitório
    Note over W,DB: EnableRetryOnFailure repete o bloco inteiro<br/>(ExecutarEmTransacaoAsync)
    alt continua falhando
        W-->>MQ: exceção
        Note over MQ: Rebus repete a mensagem (padrão: 5 tentativas)<br/>e depois a move para a fila error
    end
```

Não há evento de rejeição nem notificação ao cliente: mensagem que esgota as tentativas vai para a fila `error`, que é o ponto a monitorar. O Consolidado segue a mesma regra (relança a exceção para o Rebus repetir).

---

## 6. Implantação no compose

```mermaid
graph TB
    subgraph Compose["docker/docker-compose.yml (rede saga-network)"]
        LAPI[lancamentos-api :5200]
        WORKER[lancamentos-worker]
        CONS[fluxocaixa-consolidado]
        CAPI[fluxocaixa-api :5100]
        PGL[(fluxocaixa-postgres-lancamentos :5433)]
        PGC[(fluxocaixa-postgres-consolidado :5434)]
        REDIS[(fluxocaixa-redis :6379)]
        MQ[saga-rabbitmq :5672 / :15672]
        SEQ[saga-seq :5342]
        JAEGER[saga-jaeger :16686]
    end

    LAPI --> MQ
    LAPI --> PGL
    WORKER --> MQ
    WORKER --> PGL
    CONS --> MQ
    CONS --> PGC
    CONS --> REDIS
    CAPI --> PGC
    CAPI --> REDIS

    LAPI -.->|OTLP| JAEGER
    WORKER -.->|OTLP| JAEGER
```

O RabbitMQ, o Seq e o Jaeger são compartilhados com a SAGA. Só a API de lançamentos e o worker exportam traces para o Jaeger: o Consolidado não configura OpenTelemetry, e a API de consulta não recebe o endereço do Jaeger no compose. Não há Prometheus nem Grafana na stack.

---

## Limitações

| Ponto | Situação |
|---|---|
| Disponibilidade da escrita sem o Consolidado | A API de lançamentos e o worker não dependem do Consolidado; os eventos ficam na fila `fluxocaixa-consolidado` até ele voltar. Isso é consequência do desenho, não foi testado com falha injetada. |
| Vazão de 50 requisições por segundo na consulta | Objetivo, não medido. Existe o limitador configurado, mas nenhum teste de carga. |
| Perda de lançamentos | Não medida. Evento publicado antes de o Consolidado assinar os tópicos é descartado pelo RabbitMQ; por isso o `aguardar-stack.sh` espera as assinaturas antes dos testes. |
| Duplicidade | Não há outbox: a gravação e o envio do evento não são atômicos. Se o envio falhar depois do commit, o Rebus repete a mensagem e o worker grava o lançamento de novo, com outro `Id`. O Consolidado também aplica de novo um evento reentregue; nenhum dos dois é idempotente. |
| Escala do Consolidado | Uma mensagem por vez (`MaxParallelism = 1`), porque a linha do dia não tem controle de concorrência. |
