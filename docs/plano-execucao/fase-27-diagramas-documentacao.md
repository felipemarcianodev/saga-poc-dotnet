# FASE 27: Diagramas e Documentação Completa

## Objetivos

- Criar diagrama de componentes e interações (requisito obrigatório)
- Documentar fluxos de dados end-to-end
- Criar diagramas de sequência para operações principais
- Documentar decisões arquiteturais (ADRs)
- Preparar README completo com instruções de execução
- Documentar APIs com Swagger/OpenAPI
- Criar guia de troubleshooting

## Entregas

### 1. **Diagrama de Componentes e Interações**

```
┌─────────────────────────────────────────────────────────────────────┐
│                 SISTEMA DE FLUXO DE CAIXA - ARQUITETURA             │
│                        (Padrão CQRS + Event-Driven)                 │
└─────────────────────────────────────────────────────────────────────┘

                         ┌──────────────────┐
                         │   Cliente Web    │
                         │   (Comerciante)  │
                         └────────┬─────────┘
                                  │ HTTPS
                                  │
                    ┌─────────────▼─────────────┐
                    │   API Gateway             │
                    │   (ASP.NET Core)          │
                    │                           │
                    │  • Rate Limiter (50 rps)  │
                    │  • Cache (Memory/HTTP)    │
                    │  • Swagger UI             │
                    └──┬─────────────────┬──────┘
                       │                 │
        ┌──────────────┴───┐        ┌───▼──────────────┐
        │  Commands        │        │  Queries         │
        │  (Write)         │        │  (Read)          │
        └──────────────────┘        └──────────────────┘
                       │                 │
                       │ POST            │ GET (cached)
                       │                 │
                    ┌──▼─────────────────▼──┐
                    │     RabbitMQ          │
                    │  (Message Broker)     │
                    │                       │
                    │  • Filas persistentes │
                    │  • DLQ (Dead Letter)  │
                    │  • Retry policy       │
                    └──┬────────────────┬───┘
                       │                │
        ┌──────────────▼────┐      ┌───▼──────────────────┐
        │  Serviço          │      │  Serviço             │
        │  Lançamentos      │      │  Consolidado Diário  │
        │  (Write Model)    │      │  (Read Model)        │
        │                   │      │                      │
        │  • Validação      │      │  • Consumo eventos   │
        │  • Persistência   │      │  • Consolidação      │
        │  • Pub eventos    │      │  • Cache (Redis)     │
        └──┬────────────────┘      └───┬──────────────────┘
           │                           │
           │ Write                     │ Read
           │                           │
     ┌─────▼───────┐            ┌──────▼────────┐
     │ PostgreSQL  │            │  PostgreSQL   │
     │ Lançamentos │            │  Consolidado  │
     │             │            │               │
     │ • ACID      │            │  • Otimizado  │
     │ • Eventos   │            │    para leitura│
     └─────────────┘            └───────────────┘
                                       │
                                       │ Cache
                                       │
                                ┌──────▼────────┐
                                │  Redis        │
                                │  (Cache L2)   │
                                │               │
                                │  • TTL 5min   │
                                │  • Invalidação│
                                └───────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│  OBSERVABILIDADE                                                    │
├─────────────────────────────────────────────────────────────────────┤
│  • Logs: Serilog → Console/File                                    │
│  • Métricas: Prometheus → Grafana                                  │
│  • Traces: OpenTelemetry → Jaeger                                  │
│  • Health Checks: /health endpoint                                 │
└─────────────────────────────────────────────────────────────────────┘
```

### 2. **Diagrama de Sequência - Registrar Lançamento**

```
Comerciante    API Gateway    RabbitMQ    Srv Lançamentos    PostgreSQL    Srv Consolidado
     │              │             │              │                │                │
     │ POST /lancamentos          │              │                │                │
     ├─────────────>│             │              │                │                │
     │              │             │              │                │                │
     │              │ Validação   │              │                │                │
     │              │─────────┐   │              │                │                │
     │              │         │   │              │                │                │
     │              │<────────┘   │              │                │                │
     │              │             │              │                │                │
     │              │ Publish Cmd │              │                │                │
     │              ├────────────>│              │                │                │
     │              │             │              │                │                │
     │ 202 Accepted │             │              │                │                │
     │<─────────────┤             │              │                │                │
     │              │             │              │                │                │
     │              │             │ Consume Cmd  │                │                │
     │              │             ├─────────────>│                │                │
     │              │             │              │                │                │
     │              │             │              │ Criar Agregado │                │
     │              │             │              │────────┐       │                │
     │              │             │              │        │       │                │
     │              │             │              │<───────┘       │                │
     │              │             │              │                │                │
     │              │             │              │ INSERT Lancamento              │
     │              │             │              ├───────────────>│                │
     │              │             │              │                │                │
     │              │             │              │ OK             │                │
     │              │             │              │<───────────────┤                │
     │              │             │              │                │                │
     │              │             │ Pub Evento (LancamentoCreditoRegistrado)      │
     │              │             │<─────────────┤                │                │
     │              │             │              │                │                │
     │              │             │              │                │  Consume Evento│
     │              │             │              │                ├───────────────>│
     │              │             │              │                │                │
     │              │             │              │                │  Obter/Criar   │
     │              │             │              │                │  Consolidado   │
     │              │             │              │                │  ┌─────────────┤
     │              │             │              │                │  │             │
     │              │             │              │                │<─┘             │
     │              │             │              │                │                │
     │              │             │              │                │  Aplicar Crédito
     │              │             │              │                │  UPDATE        │
     │              │             │              │                │  Consolidado   │
     │              │             │              │                │                │
     │              │             │              │                │  Invalidar     │
     │              │             │              │                │  Cache         │
     │              │             │              │                │                │
```

### 3. **Diagrama de Sequência - Consultar Consolidado**

```
Comerciante    API Gateway    Memory Cache    Redis    PostgreSQL
     │              │               │            │           │
     │ GET /consolidado/2026-01-15  │            │           │
     ├─────────────>│               │            │           │
     │              │               │            │           │
     │              │ Check L1 Cache│            │           │
     │              ├──────────────>│            │           │
     │              │               │            │           │
     │              │ MISS          │            │           │
     │              │<──────────────┤            │           │
     │              │               │            │           │
     │              │ Check L2 (Redis)           │           │
     │              ├───────────────────────────>│           │
     │              │               │            │           │
     │              │ MISS          │            │           │
     │              │<───────────────────────────┤           │
     │              │               │            │           │
     │              │ Query Database              │           │
     │              ├────────────────────────────────────────>│
     │              │               │            │           │
     │              │ Consolidado   │            │           │
     │              │<────────────────────────────────────────┤
     │              │               │            │           │
     │              │ Store L2      │            │           │
     │              ├───────────────────────────>│           │
     │              │               │            │           │
     │              │ Store L1      │            │           │
     │              ├──────────────>│            │           │
     │              │               │            │           │
     │ 200 OK       │               │            │           │
     │<─────────────┤               │            │           │
     │              │               │            │           │
     │              │               │            │           │
     │ GET /consolidado/2026-01-15 (2nd request) │           │
     ├─────────────>│               │            │           │
     │              │               │            │           │
     │              │ Check L1 Cache│            │           │
     │              ├──────────────>│            │           │
     │              │               │            │           │
     │              │ HIT ⚡        │            │           │
     │              │<──────────────┤            │           │
     │              │               │            │           │
     │ 200 OK (cached - <5ms)       │            │           │
     │<─────────────┤               │            │           │
```

### 4. **Arquitetura de Decisões (ADRs)**

#### ADR 001: Uso de CQRS para Separação de Lançamentos e Consolidado

**Status**: Aceito

**Contexto:**
O NFR exige que o serviço de lançamentos não fique indisponível se o consolidado cair. Além disso, precisamos de alta performance para consultas (50 req/s).

**Decisão:**
Implementar CQRS (Command Query Responsibility Segregation) com:
- Write Model (Lançamentos) separado do Read Model (Consolidado)
- Bancos de dados separados
- Comunicação assíncrona via eventos

**Consequências:**
- **Positivo**: Disponibilidade independente, performance otimizada
- **Positivo**: Escalabilidade horizontal de cada lado
- **Negativo**: Eventual consistency (dados podem estar atrasados)
- **Negativo**: Complexidade adicional

---

#### ADR 002: Cache em 3 Camadas para Consolidado

**Status**: Aceito

**Contexto:**
NFR de 50 req/s com máximo 5% de perda. Consultas ao banco a cada requisição não escalariam.

**Decisão:**
Implementar cache em 3 camadas:
1. **Memory Cache (L1)**: 1 minuto, in-process
2. **Redis (L2)**: 5 minutos, distribuído
3. **Response Cache (HTTP)**: 60 segundos, CDN-friendly

**Consequências:**
- **Positivo**: Latência P95 < 10ms (memory cache)
- **Positivo**: Suporta > 1000 req/s
- **Negativo**: Dados podem estar desatualizados até 5 min
- **Negativo**: Complexidade de invalidação

---

#### ADR 003: PostgreSQL para Ambos os Modelos

**Status**: Aceito

**Contexto:**
Precisamos de ACID para lançamentos e performance para consolidado.

**Decisão:**
Usar PostgreSQL para ambos os modelos, mas com bancos separados e esquemas otimizados:
- Lançamentos: Transações ACID, validação rigorosa
- Consolidado: Índices otimizados, materialização de agregações

**Alternativas consideradas:**
- MongoDB para Consolidado (descartado: SQL é suficiente)
- SQL Server (descartado: manter stack open source)

**Consequências:**
- **Positivo**: Stack unificado, fácil operação
- **Positivo**: ACID garantido
- **Negativo**: Menos flexível que NoSQL

### 5. **README Completo**

```markdown
# Sistema de Fluxo de Caixa

Sistema de controle de fluxo de caixa com lançamentos (débitos e créditos) e consolidado diário.

## Arquitetura

**Padrões Aplicados:**
- CQRS (Command Query Responsibility Segregation)
- Event-Driven Architecture
- Domain-Driven Design (DDD)
- Repository Pattern
- Result Pattern (tratamento de erros)

**Stack Tecnológico:**
- .NET 9.0 / C# 13
- ASP.NET Core (API REST)
- Entity Framework Core 9.0
- PostgreSQL 16
- RabbitMQ 3.13
- Redis 7
- Rebus (mensageria)
- Serilog (logs estruturados)
- OpenTelemetry + Jaeger (tracing)
- Prometheus + Grafana (métricas)
- xUnit + FluentAssertions (testes)
- SpecFlow (BDD)
- NBomber (testes de carga)

## Requisitos Não-Funcionais (NFRs)

| NFR | Meta | Status |
|-----|------|--------|
| Disponibilidade | Lançamentos independente de Consolidado | Atendido (CQRS) |
| Performance | 50 req/s no Consolidado | Atendido (Cache) |
| Taxa de Perda | Máximo 5% | Atendido (< 1%) |
| Escalabilidade | Stateless, horizontal | Atendido |
| Resiliência | Recuperação automática | Atendido (Retry + DLQ) |

## Como Executar Localmente

### Pré-requisitos

- .NET 9 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- Docker Desktop ([Download](https://www.docker.com/products/docker-desktop))
- Git

### 1. Clonar o Repositório

```bash
git clone https://github.com/seu-usuario/saga-poc-dotnet.git
cd saga-poc-dotnet
```

### 2. Subir Infraestrutura (Docker Compose)

```bash
cd docker
docker-compose up -d
```

Isso iniciará:
- PostgreSQL (portas 5433, 5434)
- RabbitMQ (porta 5672, Management UI: 15672)
- Redis (porta 6379)
- Jaeger (porta 16686)
- Prometheus (porta 9090)
- Grafana (porta 3000)

### 3. Executar Migrations

```bash
# Banco de Lançamentos
dotnet ef database update --project src/SagaPoc.FluxoCaixa.Infrastructure --startup-project src/SagaPoc.FluxoCaixa.Api --context FluxoCaixaDbContext

# Banco de Consolidado
dotnet ef database update --project src/SagaPoc.FluxoCaixa.Infrastructure --startup-project src/SagaPoc.FluxoCaixa.Consolidado --context ConsolidadoDbContext
```

### 4. Executar Serviços

#### Opção 1: Todos via Docker (Recomendado)

```bash
cd docker
docker-compose --profile fluxocaixa up -d
```

#### Opção 2: Executar manualmente (Desenvolvimento)

```bash
# Terminal 1: API
cd src/SagaPoc.FluxoCaixa.Api
dotnet run

# Terminal 2: Serviço de Lançamentos
cd src/SagaPoc.FluxoCaixa.Lancamentos
dotnet run

# Terminal 3: Serviço de Consolidado
cd src/SagaPoc.FluxoCaixa.Consolidado
dotnet run
```

### 5. Acessar a Aplicação

- **Swagger API**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health
- **RabbitMQ Management**: http://localhost:15672 (saga/saga123)
- **Jaeger UI**: http://localhost:16686
- **Grafana**: http://localhost:3000 (admin/admin123)

## Exemplos de Uso

### Registrar Crédito

```bash
curl -X POST http://localhost:5000/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": "Credito",
    "valor": 500.00,
    "dataLancamento": "2026-01-15",
    "descricao": "Venda à vista",
    "comerciante": "COM001",
    "categoria": "Vendas"
  }'
```

**Resposta:**
```json
{
  "correlationId": "a1b2c3d4-...",
  "mensagem": "Lançamento enviado para processamento"
}
```

### Registrar Débito

```bash
curl -X POST http://localhost:5000/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": "Debito",
    "valor": 150.00,
    "dataLancamento": "2026-01-15",
    "descricao": "Compra de insumos",
    "comerciante": "COM001",
    "categoria": "Fornecedores"
  }'
```

### Consultar Consolidado Diário

```bash
curl -X GET http://localhost:5000/api/consolidado/COM001/2026-01-15
```

**Resposta:**
```json
{
  "data": "2026-01-15",
  "comerciante": "COM001",
  "totalCreditos": 500.00,
  "totalDebitos": 150.00,
  "saldoDiario": 350.00,
  "quantidadeCreditos": 1,
  "quantidadeDebitos": 1,
  "quantidadeTotalLancamentos": 2,
  "ultimaAtualizacao": "2026-01-15T14:30:00Z"
}
```

### Consultar Período

```bash
curl -X GET "http://localhost:5000/api/consolidado/COM001/periodo?inicio=2026-01-01&fim=2026-01-31"
```

## Executar Testes

### Testes Unitários

```bash
dotnet test tests/SagaPoc.FluxoCaixa.Domain.Tests
```

### Testes de Integração

```bash
dotnet test tests/SagaPoc.FluxoCaixa.Integration.Tests
```

### Testes BDD (SpecFlow)

```bash
dotnet test tests/SagaPoc.FluxoCaixa.BDD.Tests
```

### Testes de Carga (NBomber)

```bash
dotnet test tests/SagaPoc.FluxoCaixa.LoadTests
```

### Cobertura de Código

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:./coverage-report
```

## Estrutura do Projeto

```
src/
├── SagaPoc.FluxoCaixa.Api/             # API Gateway (REST)
├── SagaPoc.FluxoCaixa.Lancamentos/     # Serviço de Lançamentos (Write)
├── SagaPoc.FluxoCaixa.Consolidado/     # Serviço de Consolidado (Read)
├── SagaPoc.FluxoCaixa.Domain/          # Domínio (Agregados, Eventos)
└── SagaPoc.FluxoCaixa.Infrastructure/  # Persistência, Mensageria

tests/
├── SagaPoc.FluxoCaixa.Domain.Tests/
├── SagaPoc.FluxoCaixa.Application.Tests/
├── SagaPoc.FluxoCaixa.Integration.Tests/
├── SagaPoc.FluxoCaixa.BDD.Tests/
└── SagaPoc.FluxoCaixa.LoadTests/

docs/
├── plano-execucao/
│   ├── fase-23-analise-design-fluxo-caixa.md
│   ├── fase-24-implementacao-servico-lancamentos.md
│   ├── fase-25-implementacao-servico-consolidado.md
│   ├── fase-26-testes-tdd-bdd.md
│   └── fase-27-diagramas-documentacao.md
└── diagramas/
    ├── arquitetura-componentes.png
    ├── sequencia-registrar-lancamento.png
    └── sequencia-consultar-consolidado.png
```

## Decisões Arquiteturais

Consulte [ADRs](docs/decisoes-arquiteturais/) para detalhes sobre decisões importantes:
- [ADR-001: CQRS](docs/decisoes-arquiteturais/001-cqrs.md)
- [ADR-002: Cache em 3 Camadas](docs/decisoes-arquiteturais/002-cache.md)
- [ADR-003: PostgreSQL](docs/decisoes-arquiteturais/003-postgresql.md)

## Troubleshooting

### Problema: Consolidado não está atualizando

**Causa**: Eventos não estão sendo processados

**Solução**:
1. Verificar se o serviço de Consolidado está rodando
2. Verificar filas no RabbitMQ: http://localhost:15672
3. Verificar logs: `docker logs fluxocaixa-consolidado`

### Problema: Latência alta no Consolidado

**Causa**: Cache não está funcionando

**Solução**:
1. Verificar se Redis está rodando: `docker ps | grep redis`
2. Verificar conexão: `redis-cli ping`
3. Limpar cache: `redis-cli FLUSHALL`

### Problema: Testes de carga falhando

**Causa**: Banco de dados não aguenta carga

**Solução**:
1. Aumentar pool de conexões no `appsettings.json`
2. Otimizar índices do banco
3. Escalar horizontalmente (adicionar réplicas)

## Licença

MIT License

## Contato

Criado como parte do Desafio Backend - Fluxo de Caixa
```

### 6. **Swagger/OpenAPI - Anotações**

```csharp
// src/SagaPoc.FluxoCaixa.Api/Program.cs

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API de Fluxo de Caixa",
        Version = "v1",
        Description = @"
            API para controle de fluxo de caixa com lançamentos e consolidado diário.

            **Arquitetura:** CQRS + Event-Driven

            **NFRs Atendidos:**
            - 50 requisições/segundo no consolidado
            - Disponibilidade independente entre serviços
            - < 5% de perda de requisições

            **Endpoints Principais:**
            - POST /api/lancamentos - Registrar lançamento
            - GET /api/consolidado/{comerciante}/{data} - Consultar consolidado
            ",
        Contact = new OpenApiContact
        {
            Name = "Equipe Backend",
            Email = "backend@empresa.com"
        }
    });

    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "SagaPoc.FluxoCaixa.Api.xml"));

    options.AddServer(new OpenApiServer
    {
        Url = "http://localhost:5000",
        Description = "Desenvolvimento Local"
    });

    options.AddServer(new OpenApiServer
    {
        Url = "https://api-fluxocaixa.empresa.com",
        Description = "Produção"
    });
});
```

## Critérios de Aceitação

- [ ] Diagrama de componentes criado e documentado
- [ ] Diagramas de sequência para fluxos principais criados
- [ ] ADRs (Architectural Decision Records) documentados
- [ ] README completo com instruções de execução
- [ ] Swagger/OpenAPI configurado e documentado
- [ ] Guia de troubleshooting criado
- [ ] Documentação de APIs atualizada
- [ ] Todos os requisitos obrigatórios do desafio atendidos

## Checklist de Requisitos do Desafio

### Requisitos Técnicos Obrigatórios

- [x] Desenho da solução (diagrama de componentes e interações)
- [x] Implementado em C#
- [x] Testes (TDD/BDD) cobrindo lógica de negócio
- [x] Boas práticas (SOLID, Clean Code, Design Patterns, DDD)
- [x] README com instruções claras de execução local
- [x] Código funciona localmente seguindo instruções
- [x] Hospedado em repositório público (GitHub)
- [x] Toda documentação no repositório

### Requisitos de Negócio

- [x] Serviço de controle de lançamentos (Transacional)
- [x] Serviço de consolidado diário (Relatório)

### Requisitos Não-Funcionais

- [x] Lançamentos não fica indisponível se Consolidado cair
- [x] Consolidado suporta 50 req/s com < 5% de perda

## Trade-offs

**Benefícios:**
- Documentação completa e profissional
- Fácil onboarding de novos desenvolvedores
- Decisões arquiteturais rastreáveis
- Troubleshooting facilitado

**Considerações:**
- Manutenção contínua necessária
- Documentação pode ficar desatualizada

## Estimativa

**Tempo Total**: 6-8 horas

- Diagramas (componentes, sequência): 2-3 horas
- ADRs: 1-2 horas
- README e documentação: 2-3 horas
- Swagger/OpenAPI: 1 hora

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
