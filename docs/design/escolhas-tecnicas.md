# Escolhas Tecnicas

Este documento descreve as tecnologias escolhidas para esta POC e por que.

---

## Stack Utilizada

| Tecnologia | Versao | Uso |
|------------|--------|-----|
| .NET | 9.0 | Runtime e framework |
| Rebus | 8.x | Mensageria e SAGAs |
| RabbitMQ | 3.13 | Message broker |
| PostgreSQL | 16 | Banco de dados |
| Redis | 7 | Cache distribuido |
| Serilog | - | Logs estruturados |
| SEQ | - | Visualizacao de logs |
| Jaeger | - | Distributed tracing |

---

## Por que Rebus?

Escolhemos Rebus em vez de MassTransit ou NServiceBus.

**Motivo principal**: Simplicidade. Rebus e leve e tem handlers explicitos.

| Alternativa | Por que nao |
|-------------|-------------|
| MassTransit | Mais complexo, state machine integrada que nao precisavamos |
| NServiceBus | Pago, overkill para POC |
| RabbitMQ direto | Muito boilerplate |

**O que perdemos**: State machine visual, retry/circuit breaker embutido, comunidade maior.

**Limitacao encontrada**: Rebus Sagas usam InMemory por padrao. Perdemos estado ao reiniciar.

---

## Por que RabbitMQ?

Escolhemos RabbitMQ em vez de Kafka ou Azure Service Bus.

**Motivo principal**: Request/Reply nativo e Dead Letter Queue facil.

| Alternativa | Por que nao |
|-------------|-------------|
| Kafka | Overkill para SAGA, nao tem Request/Reply nativo |
| Azure Service Bus | Vendor lock-in, custo |
| In-memory | Nao persiste, nao escala |

**O que perdemos**: Alta vazao do Kafka, reprocessamento de eventos.

---

## Por que PostgreSQL?

Escolhemos PostgreSQL em vez de SQL Server, MySQL ou MongoDB.

**Motivo principal**: Open source, sem custo, bom suporte a JSON.

| Alternativa | Por que nao |
|-------------|-------------|
| SQL Server | Custo de licenca |
| MySQL | Menos recursos (sem JSONB, CTEs mais limitados) |
| MongoDB | Nao precisavamos de schema flexivel, ACID mais fraco |

**Por que 3 instancias?**
- SAGA: Estado da SAGA (hoje InMemory, mas preparado para persistencia)
- Lancamentos: Write Model do Fluxo de Caixa
- Consolidado: Read Model do Fluxo de Caixa

**Limitacao aceita**: 3 bancos e mais infraestrutura para manter.

---

## Por que Redis?

Escolhemos Redis para cache distribuido.

**Motivo principal**: Baixa latencia, suporte nativo no .NET.

**Uso**:
- Cache L2 no Consolidado (TTL 5 minutos)
- Potencial uso para idempotencia (nao implementado)

**O que nao implementamos**:
- Invalidacao por evento (fazemos apenas por TTL)
- Fallback se Redis cair

---

## Por que Serilog + SEQ + Jaeger?

**Serilog**: Logs estruturados em JSON, enrichers uteis.

**SEQ**: Visualizacao de logs com queries SQL-like. Facil de rodar via Docker.

**Jaeger**: Distributed tracing com suporte a OpenTelemetry.

**Alternativas consideradas**:
- ELK Stack: Mais pesado, mais configuracao
- Application Insights: Vendor lock-in (Azure)
- Grafana Loki: Menos maduro para .NET

---

## O Que Nao Escolhemos (e Por Que)

| Tecnologia | Por que nao |
|------------|-------------|
| Event Sourcing | Complexidade desnecessaria para POC |
| Kubernetes | Overkill, Docker Compose suficiente |
| API Gateway (Kong, Ocelot) | Nao precisavamos de rate limiting centralizado |
| gRPC | REST mais simples para demonstracao |
| GraphQL | Nao temos queries complexas |

---

## Versoes e Compatibilidade

```
.NET 9.0
Rebus 8.9.0
Rebus.RabbitMQ 9.x
PostgreSQL 16
Redis 7
RabbitMQ 3.13
```

Todas as versoes sao as mais recentes estaveis na data da criacao.

---

**Versao**: 1.0
**Data de criacao**: 2026-01-21
