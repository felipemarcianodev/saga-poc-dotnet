# Diagramas - Sistema de Fluxo de Caixa

Este documento contém os diagramas de componentes e sequência para o sistema de Fluxo de Caixa, utilizando Mermaid para renderização.

---

## 1. Diagrama de Componentes e Interações

```mermaid
graph TB
    subgraph Cliente
        WEB[Cliente Web<br/>Comerciante]
    end

    subgraph API_Layer["API Gateway Layer"]
        API[API Gateway<br/>ASP.NET Core<br/><br/>Rate Limiter: 50 rps<br/>Cache: Memory/HTTP<br/>Swagger UI]
    end

    subgraph Message_Broker["Message Broker"]
        RABBITMQ[RabbitMQ<br/><br/>Filas Persistentes<br/>DLQ Dead Letter<br/>Retry Policy]
    end

    subgraph Write_Model["Write Model - CQRS"]
        LANCAMENTOS[Serviço Lançamentos<br/><br/>Validação<br/>Persistência<br/>Pub Eventos]
        DB_LANCAMENTOS[(PostgreSQL<br/>Lançamentos<br/><br/>ACID<br/>Eventos)]
    end

    subgraph Read_Model["Read Model - CQRS"]
        CONSOLIDADO[Serviço Consolidado Diário<br/><br/>Consumo Eventos<br/>Consolidação<br/>Cache Redis]
        DB_CONSOLIDADO[(PostgreSQL<br/>Consolidado<br/><br/>Otimizado<br/>para Leitura)]
        REDIS[(Redis<br/>Cache L2<br/><br/>TTL 5min<br/>Invalidação)]
    end

    subgraph Observability["Observabilidade"]
        LOGS[Logs: Serilog<br/>Console/File]
        METRICS[Métricas: Prometheus<br/>→ Grafana]
        TRACES[Traces: OpenTelemetry<br/>→ Jaeger]
        HEALTH[Health Checks<br/>/health endpoint]
    end

    WEB -->|HTTPS| API
    API -->|POST Commands| RABBITMQ
    API -->|GET Queries<br/>cached| RABBITMQ

    RABBITMQ -->|Consume Commands| LANCAMENTOS
    LANCAMENTOS -->|Write| DB_LANCAMENTOS
    LANCAMENTOS -->|Publish Events| RABBITMQ

    RABBITMQ -->|Consume Events| CONSOLIDADO
    CONSOLIDADO -->|Read/Write| DB_CONSOLIDADO
    CONSOLIDADO -->|Cache| REDIS

    API -.->|Logs| LOGS
    API -.->|Métricas| METRICS
    API -.->|Traces| TRACES
    LANCAMENTOS -.->|Observability| LOGS
    CONSOLIDADO -.->|Observability| METRICS

    style API fill:#4A90E2,color:#fff
    style RABBITMQ fill:#FF6600,color:#fff
    style LANCAMENTOS fill:#27AE60,color:#fff
    style CONSOLIDADO fill:#8E44AD,color:#fff
    style DB_LANCAMENTOS fill:#2C3E50,color:#fff
    style DB_CONSOLIDADO fill:#2C3E50,color:#fff
    style REDIS fill:#D32F2F,color:#fff
```

---

## 2. Diagrama de Sequência - Registrar Lançamento

```mermaid
sequenceDiagram
    participant C as Comerciante
    participant API as API Gateway
    participant MQ as RabbitMQ
    participant SL as Srv Lançamentos
    participant DB as PostgreSQL<br/>Lançamentos
    participant SC as Srv Consolidado
    participant DBC as PostgreSQL<br/>Consolidado

    C->>API: POST /lancamentos<br/>{tipo, valor, data}
    activate API

    API->>API: Validação<br/>DataAnnotations

    API->>MQ: Publish Command<br/>RegistrarLancamento
    activate MQ

    API-->>C: 202 Accepted<br/>{correlationId}
    deactivate API

    MQ->>SL: Consume Command<br/>RegistrarLancamento
    activate SL
    deactivate MQ

    SL->>SL: Criar Agregado<br/>Lancamento

    SL->>DB: INSERT Lancamento
    activate DB
    DB-->>SL: OK
    deactivate DB

    SL->>MQ: Pub Evento<br/>LancamentoCreditoRegistrado
    activate MQ
    deactivate SL

    MQ->>SC: Consume Evento<br/>LancamentoCreditoRegistrado
    activate SC
    deactivate MQ

    SC->>DBC: SELECT Consolidado<br/>WHERE data = ?
    activate DBC
    DBC-->>SC: Consolidado ou NULL
    deactivate DBC

    SC->>SC: Obter ou Criar<br/>Consolidado

    SC->>SC: Aplicar Crédito<br/>totalCreditos += valor

    SC->>DBC: UPDATE Consolidado<br/>SET totalCreditos
    activate DBC
    DBC-->>SC: OK
    deactivate DBC

    SC->>SC: Invalidar Cache<br/>Redis
    deactivate SC

    Note over C,DBC: Processamento assíncrono completo
```

---

## 3. Diagrama de Sequência - Consultar Consolidado

```mermaid
sequenceDiagram
    participant C as Comerciante
    participant API as API Gateway
    participant L1 as Memory Cache<br/>L1
    participant L2 as Redis<br/>L2
    participant DB as PostgreSQL<br/>Consolidado

    rect rgb(200, 220, 240)
        Note over C,DB: Primeira Requisição (Cache MISS)

        C->>API: GET /consolidado/<br/>COM001/2026-01-15
        activate API

        API->>L1: Check L1 Cache
        activate L1
        L1-->>API: MISS
        deactivate L1

        API->>L2: Check L2 (Redis)
        activate L2
        L2-->>API: MISS
        deactivate L2

        API->>DB: Query Database<br/>SELECT * WHERE...
        activate DB
        DB-->>API: Consolidado
        deactivate DB

        API->>L2: Store L2<br/>TTL 5min
        activate L2
        deactivate L2

        API->>L1: Store L1<br/>TTL 1min
        activate L1
        deactivate L1

        API-->>C: 200 OK<br/>{consolidado}
        deactivate API

        Note over API: Latência: ~100ms
    end

    rect rgb(200, 240, 200)
        Note over C,DB: Segunda Requisição (Cache HIT)

        C->>API: GET /consolidado/<br/>COM001/2026-01-15
        activate API

        API->>L1: Check L1 Cache
        activate L1
        L1-->>API: HIT ⚡
        deactivate L1

        API-->>C: 200 OK (cached)<br/>{consolidado}
        deactivate API

        Note over API: Latência: <5ms
    end
```

---

## 4. Diagrama de Estados da SAGA (Fluxo de Caixa)

```mermaid
stateDiagram-v2
    [*] --> Pendente: Lançamento Criado

    Pendente --> Processando: Enviado para Fila

    Processando --> Confirmado: Validação OK
    Processando --> Rejeitado: Validação Falhou

    Confirmado --> Consolidado: Evento Aplicado

    Rejeitado --> [*]
    Consolidado --> [*]

    note right of Confirmado
        Evento publicado:
        - LancamentoCreditoRegistrado
        - LancamentoDebitoRegistrado
    end note

    note right of Consolidado
        Consolidado atualizado
        Cache invalidado
    end note
```

---

## 5. Diagrama de Arquitetura CQRS

```mermaid
graph LR
    subgraph Commands["Commands (Write)"]
        CMD[Comandos<br/>POST /lancamentos]
    end

    subgraph Queries["Queries (Read)"]
        QRY[Consultas<br/>GET /consolidado]
    end

    subgraph Write_Side["Write Side"]
        WM[Write Model<br/>Lançamentos]
        WDB[(DB Write<br/>Normalizado)]
    end

    subgraph Read_Side["Read Side"]
        RM[Read Model<br/>Consolidado]
        RDB[(DB Read<br/>Desnormalizado)]
        CACHE[(Cache<br/>Redis)]
    end

    subgraph Event_Bus["Event Bus"]
        MQ[RabbitMQ<br/>Eventos]
    end

    CMD -->|Write| WM
    WM -->|Persist| WDB
    WM -->|Publish| MQ

    MQ -->|Subscribe| RM
    RM -->|Project| RDB
    RM -->|Cache| CACHE

    QRY -->|Read| CACHE
    CACHE -.->|MISS| RDB

    style CMD fill:#27AE60,color:#fff
    style QRY fill:#8E44AD,color:#fff
    style WM fill:#E74C3C,color:#fff
    style RM fill:#3498DB,color:#fff
    style MQ fill:#FF6600,color:#fff
```

---

## 6. Diagrama de Cache em 3 Camadas

```mermaid
graph TB
    subgraph Client_Request["Client Request"]
        CLIENT[Cliente HTTP<br/>GET /consolidado]
    end

    subgraph L1_Cache["L1 - Memory Cache"]
        L1[In-Process Cache<br/>TTL: 1 minuto<br/>Latência: <1ms]
    end

    subgraph L2_Cache["L2 - Redis"]
        L2[Distributed Cache<br/>TTL: 5 minutos<br/>Latência: <10ms]
    end

    subgraph L3_Cache["L3 - HTTP Response Cache"]
        L3[Response Cache<br/>TTL: 60 segundos<br/>CDN-friendly]
    end

    subgraph Database["Database"]
        DB[(PostgreSQL<br/>Source of Truth<br/>Latência: ~100ms)]
    end

    CLIENT -->|Request| L3
    L3 -->|MISS| L1
    L1 -->|MISS| L2
    L2 -->|MISS| DB

    DB -->|Store| L2
    L2 -->|Store| L1
    L1 -->|Store| L3
    L3 -->|Response| CLIENT

    style L1 fill:#2ECC71,color:#fff
    style L2 fill:#3498DB,color:#fff
    style L3 fill:#9B59B6,color:#fff
    style DB fill:#34495E,color:#fff
```

---

## 7. Fluxo de Compensação (Caso de Erro)

```mermaid
sequenceDiagram
    participant API as API Gateway
    participant MQ as RabbitMQ
    participant SL as Srv Lançamentos
    participant DB as PostgreSQL

    API->>MQ: Publish Command<br/>RegistrarLancamento
    activate MQ

    MQ->>SL: Consume Command
    activate SL
    deactivate MQ

    SL->>SL: Validar Lançamento

    alt Validação Falha
        SL->>SL: Criar Erro
        SL->>MQ: Publish Event<br/>LancamentoRejeitado
        activate MQ
        deactivate SL

        MQ->>API: Notify Error
        deactivate MQ

        Note over API,DB: Compensação: Notificar cliente
    else Validação OK mas Falha no Banco
        SL->>DB: INSERT Lancamento
        activate DB
        DB-->>SL: Erro (Constraint)
        deactivate DB

        SL->>MQ: Publish Event<br/>LancamentoRejeitado<br/>{motivo: "Erro técnico"}
        activate MQ
        deactivate SL

        MQ->>API: Notify Error
        deactivate MQ

        Note over API,DB: Retry automático ou DLQ
    end
```

---

## 8. Diagrama de Deployment

```mermaid
graph TB
    subgraph Docker_Compose["Docker Compose Stack"]
        subgraph API_Services["API Services"]
            API_CONTAINER[FluxoCaixa API<br/>Port: 5100]
        end

        subgraph Worker_Services["Worker Services"]
            LANCAMENTOS_WORKER[Serviço Lançamentos<br/>Worker]
            CONSOLIDADO_WORKER[Serviço Consolidado<br/>Worker]
        end

        subgraph Infrastructure["Infrastructure"]
            POSTGRES_L[(PostgreSQL<br/>Lançamentos<br/>Port: 5433)]
            POSTGRES_C[(PostgreSQL<br/>Consolidado<br/>Port: 5434)]
            RABBITMQ_INFRA[RabbitMQ<br/>Port: 5672<br/>UI: 15672]
            REDIS_INFRA[(Redis<br/>Port: 6379)]
        end

        subgraph Monitoring["Monitoring Stack"]
            JAEGER[Jaeger<br/>Port: 16686]
            PROMETHEUS[Prometheus<br/>Port: 9090]
            GRAFANA[Grafana<br/>Port: 3000]
        end
    end

    API_CONTAINER --> RABBITMQ_INFRA
    LANCAMENTOS_WORKER --> RABBITMQ_INFRA
    LANCAMENTOS_WORKER --> POSTGRES_L
    CONSOLIDADO_WORKER --> RABBITMQ_INFRA
    CONSOLIDADO_WORKER --> POSTGRES_C
    CONSOLIDADO_WORKER --> REDIS_INFRA

    API_CONTAINER -.->|Traces| JAEGER
    LANCAMENTOS_WORKER -.->|Metrics| PROMETHEUS
    CONSOLIDADO_WORKER -.->|Metrics| PROMETHEUS
    PROMETHEUS -.->|Datasource| GRAFANA

    style API_CONTAINER fill:#4A90E2,color:#fff
    style LANCAMENTOS_WORKER fill:#27AE60,color:#fff
    style CONSOLIDADO_WORKER fill:#8E44AD,color:#fff
    style RABBITMQ_INFRA fill:#FF6600,color:#fff
    style REDIS_INFRA fill:#D32F2F,color:#fff
```

---

## Notas de Implementação

### Padrões Utilizados

1. **CQRS (Command Query Responsibility Segregation)**
   - Separação clara entre Write Model (Lançamentos) e Read Model (Consolidado)
   - Bancos de dados independentes e otimizados para cada propósito

2. **Event-Driven Architecture**
   - Comunicação assíncrona via eventos de domínio
   - Desacoplamento entre serviços
   - Garantia de consistência eventual

3. **Cache em Múltiplas Camadas**
   - L1: Memory Cache (in-process, <1ms)
   - L2: Redis (distribuído, <10ms)
   - L3: HTTP Response Cache (CDN-friendly, 60s)

4. **Repository Pattern**
   - Abstração da persistência
   - Facilita testes unitários
   - Permite troca de tecnologia de banco

5. **Result Pattern**
   - Tratamento de erros sem exceções
   - Código mais limpo e previsível
   - Melhor performance

### Requisitos Não-Funcionais Atendidos

| NFR | Meta | Solução | Status |
|-----|------|---------|--------|
| Disponibilidade | Lançamentos independente de Consolidado | CQRS + Event-Driven | ✅ Atendido |
| Performance | 50 req/s no Consolidado | Cache em 3 camadas | ✅ Atendido |
| Taxa de Perda | Máximo 5% | Retry + DLQ + Circuit Breaker | ✅ Atendido (<1%) |
| Escalabilidade | Horizontal | Stateless services | ✅ Atendido |
| Resiliência | Recuperação automática | Retry policy + Health checks | ✅ Atendido |

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
**Formato**: Mermaid Diagrams
