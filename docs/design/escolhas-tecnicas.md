# Escolhas Técnicas

Este documento descreve as tecnologias escolhidas para esta POC e por quê.

---

## Stack Utilizada

| Tecnologia | Versão | Uso |
|------------|--------|-----|
| .NET / C# | 10 / 14 | Runtime e linguagem (`net10.0` no `Directory.Build.props`) |
| Rebus | 8.9.5 | Mensageria e SAGA (`Rebus.RabbitMq` 10.1.2, `Rebus.PostgreSql` 9.1.1, `Rebus.ServiceProvider` 10.7.2) |
| RabbitMQ | 3.13 | Message broker |
| PostgreSQL | 16 | Estado da SAGA e os dois bancos do fluxo de caixa |
| EF Core + Npgsql | 10 | Persistência do fluxo de caixa (a SAGA não usa EF) |
| Redis | 7 | Cache distribuído da consulta do fluxo de caixa |
| Serilog | `Serilog.AspNetCore` 10 | Logs estruturados |
| Seq | imagem `latest` | Visualização de logs |
| OpenTelemetry | 1.20 | Traces, exportados por OTLP gRPC |
| Jaeger | `all-in-one:latest` | Visualização de traces (recebe OTLP na porta 4317) |
| Polly | 8.8 | Só na variante `ServicoPagamentoComPolly`, que não está registrada |

As imagens `latest` de Seq e Jaeger não estão fixadas; a do Jaeger é da linha 1, já encerrada. Fixar a versão ou migrar para o Jaeger 2 está no backlog.

---

## Por que Rebus?

Escolhemos Rebus em vez de MassTransit ou NServiceBus.

**Motivo principal**: simplicidade. O Rebus é leve, e a SAGA é uma classe com um handler explícito por mensagem.

| Alternativa | Prós | Por que não |
|-------------|------|-------------|
| MassTransit | State machine declarativa, retry e circuit breaker embutidos | Mais conceitos para aprender; a state machine não era necessária para um fluxo linear |
| NServiceBus | Mais maduro, suporte comercial | Pago, excessivo para uma POC |
| RabbitMQ direto | Controle total | Muito boilerplate (serialização, roteamento, retry, correlação) |

**O que perdemos**: state machine declarativa, retry com espera e circuit breaker prontos, comunidade maior.

**Como o estado é persistido**: o próprio Rebus guarda a SAGA no PostgreSQL (`StoreInPostgres`), nas tabelas `pedido_saga_data` e `pedido_saga_index`, que ele cria ao subir. Duas consequências:
- Nenhum outro componente pode criar ou alterar essas tabelas. Uma tabela com o mesmo nome criada pelo EF Core, com outro esquema, quebrou a SAGA até 2026-10-17.
- `MarkAsComplete()` apaga a linha da SAGA concluída. O desfecho não fica guardado, e por isso a consulta de status do pedido ainda é mock.

**Limitação encontrada**: sem instrumentação do Rebus configurada, a SAGA não aparece no Jaeger, e o contexto do trace não atravessa o RabbitMQ.

---

## Por que RabbitMQ?

Escolhemos RabbitMQ em vez de Kafka ou Azure Service Bus.

**Motivo principal**: o padrão comando/resposta da SAGA (`Send`/`Reply`) e o Publish/Subscribe do fluxo de caixa se encaixam bem em filas e exchanges, e o Rebus tem transporte maduro para ele (com fila `error` para mensagens que esgotam as tentativas).

| Alternativa | Por que não |
|-------------|-------------|
| Kafka | Pensado para log de eventos e alta vazão; comando/resposta exige montar à mão |
| Azure Service Bus | Vendor lock-in, custo, não roda localmente no compose |
| In-memory | Não persiste, não escala |

**O que perdemos**: a vazão do Kafka e o reprocessamento de eventos antigos (replay).

---

## Por que PostgreSQL?

Escolhemos PostgreSQL em vez de SQL Server, MySQL ou MongoDB.

**Motivo principal**: open source, sem custo, suportado pelo Rebus (estado da SAGA) e pelo EF Core (fluxo de caixa).

| Alternativa | Por que não |
|-------------|-------------|
| SQL Server | Custo de licença |
| MySQL | Menos recursos (sem JSONB, CTEs mais limitadas) |
| MongoDB | Não precisávamos de schema flexível; ACID mais fraco |

**Por que 3 instâncias?**
- SAGA (5432): estado da SAGA, persistido pelo Rebus
- Lançamentos (5433): write model do fluxo de caixa, migrado pelo `Lancamentos.Worker`
- Consolidado (5434): read model do fluxo de caixa, migrado pelo `FluxoCaixa.Consolidado`

**Limitação aceita**: 3 bancos são mais infraestrutura para manter. Detalhes em [ADR 003](../decisoes-tecnicas/003-postgresql.md).

---

## Por que Redis?

Escolhemos Redis para o cache distribuído da consulta do fluxo de caixa.

**Motivo principal**: baixa latência e suporte nativo no .NET (`AddStackExchangeRedisCache`).

**Uso**:
- A API de consulta grava o consolidado do dia no Redis (TTL de 5 minutos), atrás de um cache em memória de 1 minuto.
- O Consolidado apaga a chave do dia a cada evento aplicado (mesmo prefixo `FluxoCaixa:` da API).

**Limitações**:
- A invalidação vale só para o Redis: o cache em memória e o response cache (60 s) da API de consulta não são invalidados, então a leitura pode vir defasada até cerca de 60 s.
- Erros do Redis são registrados em log e a leitura segue para o banco. Se a remoção da chave falhar no Consolidado, o valor antigo fica até o TTL vencer. Nenhum dos dois casos foi testado com o Redis fora do ar.
- Não é usado para idempotência.

---

## Por que Serilog + Seq + Jaeger?

**Serilog**: logs estruturados, com enrichers (aplicação, ambiente, máquina, thread, processo).

**Seq**: visualização de logs com consultas por propriedade. Fácil de rodar via Docker.

**Jaeger**: visualização de traces; recebe OTLP do OpenTelemetry (não há exportador Jaeger nativo no código).

**Alternativas consideradas**:
- ELK Stack: mais pesado, mais configuração
- Application Insights: vendor lock-in (Azure)
- Grafana Loki + Tempo: alternativa viável, com mais componentes para subir

**Limitações atuais**: só as três APIs mandam log para o Seq (os workers ficam no console), e só `SagaPoc.Api`, `Lancamentos.Api` e `Lancamentos.Worker` geram spans. Detalhes em [arquitetura.md](../arquitetura.md#observabilidade).

---

## O Que Não Escolhemos (e Por Quê)

| Tecnologia | Por que não |
|------------|-------------|
| Event Sourcing | Complexidade desnecessária para a POC |
| Kubernetes | Excessivo; Docker Compose é suficiente |
| API Gateway (Kong, Ocelot) | Não precisávamos de roteamento nem de rate limiting centralizados |
| gRPC entre serviços | Os serviços conversam só por mensageria; as APIs de entrada usam REST |
| GraphQL | Não temos consultas complexas |

---

**Última atualização**: 2026-10-20 (versões do .NET 10, estado da SAGA no PostgreSQL pelo Rebus, cache e observabilidade como estão hoje)
