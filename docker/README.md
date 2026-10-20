# Stack Docker

O `docker-compose.yml` deste diretório sobe o sistema inteiro: a SAGA de pedidos, o fluxo de caixa (escrita e consulta), RabbitMQ, os três PostgreSQL, o Redis e a observabilidade (Seq e Jaeger). Todos os comandos abaixo rodam **a partir da raiz do repositório**.

## Pré-requisitos

- Docker com o plugin Compose v2 (`docker compose`, sem hífen)
- Git Bash ou outro shell POSIX para os scripts de `docs/scripts/`

## Serviços

| Container | Papel | Porta no host |
|---|---|---|
| `saga-api` | API de pedidos (entrada da SAGA); Swagger na raiz | 5000 |
| `saga-orquestrador` | `PedidoSaga` (Rebus); estado no `saga-postgres` | — |
| `saga-servico-restaurante`, `saga-servico-pagamento`, `saga-servico-entregador`, `saga-servico-notificacao` | serviços simulados da SAGA | — |
| `lancamentos-api` | API de lançamentos (escrita do fluxo de caixa) | 5200 |
| `lancamentos-worker` | grava o lançamento e publica o evento; migra `fluxocaixa_lancamentos` | — |
| `fluxocaixa-consolidado` | assina os eventos e mantém o saldo diário; migra `fluxocaixa_consolidado` | — |
| `fluxocaixa-api` | API de consulta do consolidado | 5100 |
| `saga-postgres` | banco `sagapoc` (estado da SAGA) | 5432 |
| `fluxocaixa-postgres-lancamentos` | banco `fluxocaixa_lancamentos` | 5433 |
| `fluxocaixa-postgres-consolidado` | banco `fluxocaixa_consolidado` | 5434 |
| `fluxocaixa-redis` | cache do consolidado | 6379 |
| `saga-rabbitmq` | broker (AMQP 5672; management em http://localhost:15672) | 5672, 15672 |
| `saga-seq` | logs estruturados, UI em http://localhost:5342 | 5342 |
| `saga-jaeger` | traces, UI em http://localhost:16686 | 16686 |

Usuário e senha: `saga` / `saga123` nos três PostgreSQL e no RabbitMQ; `admin` / `admin123` no Seq (definidos na primeira subida do volume).

## Operação

```bash
# Subir (ou recriar o que mudou) com build das imagens
docker compose -f docker/docker-compose.yml up -d --build

# Esperar filas, assinaturas e APIs ficarem prontas (sai 1 se o tempo acabar)
bash docs/scripts/aguardar-stack.sh

# Estado dos containers
docker compose -f docker/docker-compose.yml ps

# Logs de um serviço (use o nome do serviço no compose ou o do container)
docker compose -f docker/docker-compose.yml logs -f saga-orquestrador
docker logs saga-orquestrador --since 10m

# Rebuild de um serviço só
docker compose -f docker/docker-compose.yml up -d --build saga-api

# Derrubar (com -v, apaga também os volumes de dados)
docker compose -f docker/docker-compose.yml down
docker compose -f docker/docker-compose.yml down -v
```

Os Dockerfiles copiam `src/` inteiro e o `Directory.Build.props`; por isso o contexto de build é a raiz (`context: ..` no compose).

Validação de ponta a ponta, com a stack no ar:

```bash
bash docs/scripts/testar-casos-de-uso.sh
bash docs/scripts/testar-fluxo-caixa.sh
```

O que cada script confere está em [docs/scripts/readme-script.md](../docs/scripts/readme-script.md).

## Rede

Todos os serviços estão na rede `saga-network` e se enxergam pelo **nome do serviço no compose**, não pelo nome do container: `postgres:5432`, `postgres-fluxocaixa:5432`, `postgres-consolidado:5432`, `redis:6379`, `rabbitmq:5672`, `seq:80`, `jaeger:4317`.

O OTLP do Jaeger (4317/4318) não é publicado no host. Um serviço rodando fora do Docker não exporta traces, a menos que se publique a porta.

## Consultas úteis

### Estado da SAGA

O Rebus cria e mantém `pedido_saga_data` e `pedido_saga_index`. A coluna `data` é JSON gravado em `bytea`. SAGA concluída ou compensada é removida por `MarkAsComplete()`, então a tabela só mostra pedidos em andamento (ou travados). Não crie nem altere essas tabelas à mão.

```bash
# SAGAs em andamento
docker exec saga-postgres psql -U saga -d sagapoc -c \
  "SELECT id, revision, convert_from(data, 'UTF8')::json->>'EstadoAtual' AS estado FROM pedido_saga_data;"

# Detalhe de uma SAGA (o id é o PedidoId devolvido pela API)
docker exec saga-postgres psql -U saga -d sagapoc -c \
  "SELECT convert_from(data, 'UTF8') FROM pedido_saga_data WHERE id = '<PedidoId>';"
```

O desfecho de um pedido (concluído ou compensado) só aparece no log do orquestrador:

```bash
docker logs saga-orquestrador 2>&1 | grep '<PedidoId>'
```

### Fluxo de caixa

```bash
# Lançamentos gravados (colunas em snake_case)
docker exec fluxocaixa-postgres-lancamentos psql -U saga -d fluxocaixa_lancamentos -c \
  "SELECT id, tipo, valor, data_lancamento, comerciante, status FROM lancamentos ORDER BY criado_em DESC LIMIT 10;"

# Consolidado diário (tabela e colunas em PascalCase, por isso entre aspas)
docker exec fluxocaixa-postgres-consolidado psql -U saga -d fluxocaixa_consolidado -c \
  'SELECT "Comerciante", "Data", "TotalCreditos", "TotalDebitos" FROM "ConsolidadosDiarios" ORDER BY "UltimaAtualizacao" DESC LIMIT 10;'
```

### RabbitMQ

```bash
# Filas com mensagens prontas, em processamento e consumidores
docker exec saga-rabbitmq rabbitmqctl list_queues -q name messages_ready messages_unacknowledged consumers

# Assinaturas dos eventos do fluxo de caixa (devem aparecer duas, para fluxocaixa-consolidado)
docker exec saga-rabbitmq rabbitmqctl list_bindings -q source_name destination_name routing_key | grep RebusTopics
```

Mensagem na fila `error` é sempre bug. Diagnóstico nos runbooks: [SAGA](../docs/runbook-troubleshooting.md) e [fluxo de caixa](../docs/troubleshooting-fluxo-caixa.md).

## Problemas na subida

- **Porta ocupada:** confira quem usa a porta (`netstat -ano | findstr :5432` no Windows) e libere-a ou mude o mapeamento no compose.
- **Container reiniciando:** `docker logs <container>` mostra a exceção da subida. Os serviços só sobem depois que PostgreSQL, RabbitMQ e Redis passam no healthcheck (`depends_on: condition: service_healthy`).
- **Erro de GSS/Kerberos no log:** as connection strings do compose usam `GSS Encryption Mode=Disable` porque a imagem `aspnet:10.0` não tem `libgssapi_krb5.so.2`. Uma connection string nova sem esse parâmetro volta a imprimir o erro.

## Variáveis de ambiente

As configurações estão no `docker-compose.yml` (connection strings, `RabbitMQ__*`, `Otlp__*`, `Seq__*`). A única variável lida do ambiente do host é `SIMULAR_HORARIO_PICO` (padrão `true`): com `false`, o entregador deixa de recusar 30% das alocações entre 12h e 14h59 UTC. O CI usa `false`.

```bash
SIMULAR_HORARIO_PICO=false docker compose -f docker/docker-compose.yml up -d saga-servico-entregador
```

As senhas ficam em texto plano no compose porque a stack é local e de demonstração.
