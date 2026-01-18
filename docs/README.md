# POC SAGA Pattern + CQRS com Rebus e RabbitMQ

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp)
![Rebus](https://img.shields.io/badge/Rebus-8.9-blue)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3.13-FF6600?logo=rabbitmq)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)

**Proof of Concept educacional** demonstrando:
- **SAGA Orquestrado** para Delivery com compensações em cascata
- **CQRS + Event-Driven** para Fluxo de Caixa com cache em 3 camadas

Utilizando **Rebus** e **RabbitMQ** para comunicação entre microsserviços, com **Result Pattern** para tratamento estruturado de erros.

---
## Por que esse repositório existe

Esse código **não nasceu para ser usado em produção**.

Ele nasceu porque explicar Sagas apenas em texto não estava sendo suficiente.
Sempre que o assunto surgia, a conversa escorregava para mensageria, tooling ou framework.
E o problema real ficava de fora.

Essa PoC existe para tornar visível uma dor comum em sistemas distribuídos:

> o fluxo funciona, mas o significado não está claro.

---

## O problema que este código tenta expor

Sagas coordenam execução.
Elas **não garantem entendimento**.

Na prática, a dificuldade raramente está em:

* publicar eventos
* reagir a mensagens
* implementar compensações

Ela aparece quando:

* contratos são implícitos
* versionamento vira formalidade
* o domínio é interpretado de formas diferentes por serviços distintos

Esse repositório usa Saga como instrumento didático para **provocar essa discussão**, não para encerrá‑la.

---

## O que você vai encontrar aqui

* Um fluxo distribuído modelado com Saga
* Comunicação assíncrona usando mensageria
* Compensações explícitas para falhas intermediárias
* Código organizado para leitura e análise — não otimizado para produção

Tudo funciona.
E isso é parte do problema.

---

## O que você **não** vai encontrar

* Arquitetura de referência
* Best practices universais
* Respostas fechadas
* Promessa de escalabilidade infinita

Existem decisões aqui que são **intencionalmente discutíveis**.
Elas fazem parte do aprendizado.

---

## Sobre contratos e versionamento

Mensagens aqui são versionadas.
Eventos têm nomes claros.

Ainda assim, o código não responde perguntas como:

* esse evento é contrato ou coincidência?
* quem decide o significado de um campo?
* quando uma mudança quebra entendimento, mesmo sem quebrar execução?

Essas perguntas não são resolvidas por tooling.

---

## Como usar este repositório

* Leia o código como quem lê uma decisão
* Questione os limites do fluxo
* Observe onde o domínio começa a ficar nebuloso

Se você discordar de algo aqui, o objetivo foi cumprido.

---

## Sobre o Projeto

### Domínio
Sistema de **Delivery de Comida** simulando um fluxo completo de processamento de pedidos.

### Objetivo
Demonstrar como implementar:
- **SAGA Orquestrado** com Rebus Sagas
- **Compensações explícitas** em caso de falha
- **Result Pattern** para tratamento de erros sem exceções
- **Mensageria assíncrona** com RabbitMQ
- **Idempotência** nas operações de compensação
- **Stack 100% Open Source** - Sem dependências de cloud

---

## Arquitetura

### Fluxo da SAGA

```mermaid
flowchart TD
    Cliente([Cliente/Postman]) -->|POST /api/pedidos| API[🌐 API REST<br/>SagaPoc.Api]

    API -->|Envia comando<br/>IniciarPedido| SAGA[🎭 SAGA Orquestrador<br/>Rebus Saga]

    subgraph estados[Estados da SAGA]
        direction TB
        E1[1️⃣ ValidandoRestaurante]
        E2[2️⃣ ProcessandoPagamento]
        E3[3️⃣ AlocandoEntregador]
        E4[4️⃣ NotificandoCliente]
        E5A[✅ PedidoConfirmado]
        E5B[❌ PedidoCancelado]

        E1 --> E2 --> E3 --> E4 --> E5A
        E1 -.->|Falha| E5B
        E2 -.->|Falha| E5B
        E3 -.->|Falha| E5B
    end

    SAGA --> estados

    SAGA -->|Send via| BUS{🐰 RabbitMQ<br/>Message Broker}

    BUS -->|ValidarPedidoRestaurante| S1[🏪 Serviço Restaurante]
    BUS -->|ProcessarPagamento| S2[💳 Serviço Pagamento]
    BUS -->|AlocarEntregador| S3[🚚 Serviço Entregador]
    BUS -->|NotificarCliente| S4[🔔 Serviço Notificação]

    S1 -->|Reply/Compensação| BUS
    S2 -->|Reply/Compensação| BUS
    S3 -->|Reply/Compensação| BUS
    S4 -->|Reply| BUS

    BUS -.->|Atualiza estado| SAGA

    style API fill:#bbdefb,stroke:#1976d2,stroke-width:3px
    style SAGA fill:#e1bee7,stroke:#7b1fa2,stroke-width:3px
    style BUS fill:#ffe0b2,stroke:#f57c00,stroke-width:3px
    style S1 fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style S2 fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style S3 fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style S4 fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style estados fill:#fce4ec,stroke:#c2185b,stroke-width:2px
    style E5A fill:#a5d6a7,stroke:#2e7d32,stroke-width:3px
    style E5B fill:#ef9a9a,stroke:#c62828,stroke-width:3px
```

### Compensações em Cascata

Quando ocorre uma falha em qualquer etapa, as compensações são executadas **em ordem reversa**:

```mermaid
flowchart TD
    Start[❌ Falha em Qualquer Etapa] --> Decision{Qual etapa<br/>falhou?}

    Decision -->|Validação Restaurante| C1[✅ Sem compensação<br/>SAGA Cancelada]

    Decision -->|Pagamento| C2A[⬅️ Cancelar Pedido<br/>no Restaurante]
    C2A --> C2B[✅ SAGA Cancelada]

    Decision -->|Alocação Entregador| C3A[⬅️ 1. Estornar Pagamento]
    C3A --> C3B[⬅️ 2. Cancelar Pedido<br/>no Restaurante]
    C3B --> C3C[✅ SAGA Cancelada]

    Decision -->|Notificação| C4[⚠️ Pedido continua OK<br/>Sem compensação]

    style Start fill:#ffcdd2,stroke:#c62828,stroke-width:3px
    style C1 fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style C2B fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style C3C fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style C4 fill:#fff9c4,stroke:#f9a825,stroke-width:2px
    style C2A fill:#ffe0b2,stroke:#f57c00,stroke-width:2px
    style C3A fill:#ffe0b2,stroke:#f57c00,stroke-width:2px
    style C3B fill:#ffe0b2,stroke:#f57c00,stroke-width:2px
    style Decision fill:#e1bee7,stroke:#7b1fa2,stroke-width:2px
```
---

## Estrutura do Projeto

```mermaid
graph TD
    Root[📁 saga-poc-dotnet]
    Root --> Sln[📄 SagaPoc.sln]

    Root --> Docs[📂 docs/]
    Docs --> Doc1[📄 plano-execucao/]
    Doc1 --> Doc2[📄 arquitetura.md]
    Doc2 --> Doc3[📄 guia-rebus.md]
    Doc3 --> Doc4[⭐ casos-uso.md<br/> Cenários]

    Root --> Docker[📂 docker/]
    Docker --> DC[📄 docker-compose.yml]

    Root --> Src[📂 src/]

    Src --> BB[📂 BuildingBlocks/]
    BB --> Common[📦 SagaPoc.Common<br/>Result Pattern, Mensagens]
    BB --> Obs[📦 SagaPoc.Observability<br/>OpenTelemetry, Serilog]
    BB --> Infra[📦 SagaPoc.Infrastructure<br/>Implementações]
    BB --> InfraCore[📦 SagaPoc.Infrastructure.Core<br/>Interfaces]
    BB --> WebHost[📦 WebHost<br/>Extensions, Swagger]

    Src --> Api[🌐 SagaPoc.Api<br/>:5000 - API SAGA]
    Src --> Orch[🎭 SagaPoc.Orquestrador<br/>SAGA State Machine]
    Src --> Rest[🏪 SagaPoc.ServicoRestaurante]
    Src --> Pag[💳 SagaPoc.ServicoPagamento]
    Src --> Ent[🚚 SagaPoc.ServicoEntregador]
    Src --> Not[🔔 SagaPoc.ServicoNotificacao]

    Src --> FC[📂 SagaPoc.ServicoFluxoCaixa/]
    FC --> FCApi[🌐 FluxoCaixa.Api<br/>:5100 - API CQRS]
    FC --> FCDomain[📦 FluxoCaixa.Domain<br/>Agregados, Eventos]
    FC --> FCInfra[📦 FluxoCaixa.Infrastructure<br/>Repositórios, DbContext]
    FC --> FCLanc[⚡ FluxoCaixa.Lancamentos<br/>Write Handlers]
    FC --> FCCons[📊 FluxoCaixa.Consolidado<br/>Read Handlers, Cache]

    style Root fill:#e3f2fd,stroke:#1976d2,stroke-width:3px
    style Docs fill:#f3e5f5,stroke:#7b1fa2,stroke-width:2px
    style Docker fill:#fff3e0,stroke:#f57c00,stroke-width:2px
    style Src fill:#e8f5e9,stroke:#388e3c,stroke-width:2px
    style BB fill:#fff9c4,stroke:#f9a825,stroke-width:2px
    style FC fill:#e0f7fa,stroke:#00838f,stroke-width:2px
    style Api fill:#bbdefb,stroke:#1976d2,stroke-width:2px
    style FCApi fill:#b2ebf2,stroke:#00838f,stroke-width:2px
    style Orch fill:#e1bee7,stroke:#7b1fa2,stroke-width:2px
    style Rest fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style Pag fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style Ent fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
    style Not fill:#c8e6c9,stroke:#388e3c,stroke-width:2px
```
---

## Como Executar

### Pré-requisitos

- **.NET 9 SDK** ou superior ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Docker Desktop** ([Download](https://www.docker.com/products/docker-desktop))
- **Git**

### 1. Clonar o Repositório

```bash
git clone https://github.com/seu-usuario/saga-poc-dotnet.git
cd saga-poc-dotnet
```

### 2. Executar com Docker Compose (Recomendado)

Na pasta `docker/`, execute:

```bash
cd docker
docker-compose up -d
```

Isso irá iniciar toda a stack:
- **RabbitMQ 3.13** (Message Broker) - porta 5672 (AMQP) e 15672 (Management UI)
- **PostgreSQL 16** (3 instâncias) - portas 5432, 5433, 5434
- **Redis 7** (Cache distribuído) - porta 6379
- **SEQ** (Logs Estruturados) - porta 5341 (UI)
- **Jaeger** (Distributed Tracing) - porta 16686 (UI)
- **9 serviços .NET**:
  - SAGA: API (5000) + Orquestrador + 4 Workers
  - Fluxo de Caixa: API (5100) + Lançamentos + Consolidado

**OU** executar apenas o RabbitMQ (para rodar os serviços .NET manualmente):

```bash
cd docker
docker-compose up -d rabbitmq
```

### 3. Acessar as Interfaces Web

Após executar o `docker-compose up -d`, acesse:

#### RabbitMQ Management UI
```
http://localhost:15672
```
**Credenciais:** `saga` / `saga123`

Monitore:
- **Queues** (filas de mensagens)
- **Exchanges** (roteadores de mensagens)
- **Connections** (conexões ativas)
- **Channels** (canais de comunicação)

#### Jaeger UI (Distributed Tracing)
```
http://localhost:16686
```
Visualize traces distribuídos das SAGAs end-to-end:
- Selecione o serviço (ex: `SagaPoc.Api`)
- Veja a propagação através de todos os serviços
- Analise latências e bottlenecks

#### SEQ (Logs Estruturados)
```
http://localhost:5341
```
**Credenciais:** `admin` / `admin123`

Visualize logs estruturados em tempo real:
- Filtros poderosos com sintaxe SQL-like
- Queries por properties (ex: `Application = "SagaPoc.Orquestrador"`)
- Acompanhe eventos de domínio, comandos e sagas
- Correlação por CorrelationId

#### API Swagger
```
http://localhost:5000
```
Documentação interativa da API REST

### 4. Executar os Serviços

> **Nota**: Se você executou `docker-compose up -d` no passo 2, os serviços já estão rodando! Pule para a seção de testes.

#### Opção 1: Via Docker Compose (Recomendado - Já configurado)

```bash
cd docker
docker-compose up -d
```

#### Opção 2: Manualmente (9 terminais - Para desenvolvimento local)

**SAGA Delivery (6 serviços):**
```bash
# Terminal 1: API SAGA
cd src/SagaPoc.Api
dotnet run

# Terminal 2: Orquestrador
cd src/SagaPoc.Orquestrador
dotnet run

# Terminal 3: Serviço Restaurante
cd src/SagaPoc.ServicoRestaurante
dotnet run

# Terminal 4: Serviço Pagamento
cd src/SagaPoc.ServicoPagamento
dotnet run

# Terminal 5: Serviço Entregador
cd src/SagaPoc.ServicoEntregador
dotnet run

# Terminal 6: Serviço Notificação
cd src/SagaPoc.ServicoNotificacao
dotnet run
```

**Fluxo de Caixa - CQRS (3 serviços):**
```bash
# Terminal 7: API Fluxo de Caixa
cd src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api
dotnet run

# Terminal 8: Lançamentos (Write Model)
cd src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Lancamentos
dotnet run

# Terminal 9: Consolidado (Read Model)
cd src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado
dotnet run
```

### 5. Acessar as APIs

**SAGA Delivery:**
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health

**Fluxo de Caixa (CQRS):**
- **Swagger UI**: http://localhost:5100/swagger
- **Health Check**: http://localhost:5100/health

### 6. Monitorar as Filas no RabbitMQ

Acesse o **RabbitMQ Management UI** em http://localhost:15672 e clique na aba **Queues**.

Você verá as seguintes filas sendo criadas automaticamente pelo Rebus:

**SAGA Delivery:**
- **`fila-orquestrador`** - Mensagens para a SAGA (Orquestrador)
- **`fila-restaurante`** - Mensagens para validação de pedidos no restaurante
- **`fila-pagamento`** - Mensagens para processamento de pagamentos
- **`fila-entregador`** - Mensagens para alocação de entregadores
- **`fila-notificacao`** - Mensagens para notificações aos clientes

**Fluxo de Caixa (CQRS):**
- **`fila-lancamentos`** - Comandos de registro de lançamentos (Write Model)
- **`fila-consolidado`** - Eventos para atualização do consolidado (Read Model)

**Comum:**
- **`fila-error`** - Mensagens que falharam após todas as tentativas de retry (Dead Letter Queue)

Ao fazer requisições à API, você poderá ver em tempo real:
- **Ready**: Mensagens aguardando processamento
- **Unacked**: Mensagens sendo processadas no momento
- **Total**: Total de mensagens que passaram pela fila

### 7. Parar os Serviços

#### Parar toda a stack Docker
```bash
cd docker
docker-compose down
```

#### Parar e limpar volumes (remove dados persistidos)
```bash
cd docker
docker-compose down -v
```

#### Parar apenas serviços .NET (se rodando manualmente)
Pressione `Ctrl+C` em cada terminal.

---

## Testando os Casos de Uso

### 18 Cenários Implementados

O projeto possui **18 cenários de teste** divididos em dois contextos:

| Contexto | Cenários | Descrição |
|----------|----------|-----------|
| **SAGA Delivery** | 1-12 | Pedidos, falhas, compensações |
| **Fluxo de Caixa** | 13-18 | Lançamentos, consolidado, cache |

![18 Cenários implementados](./images/18-cenarios-implementados.png)

---

### Via Scripts Automatizados

#### Windows (PowerShell):
```powershell
cd docs/scripts
.\testar-casos-de-uso.ps1        # Testa todos os 18 casos
.\testar-casos-de-uso.ps1 5      # Testa apenas o caso 5 (SAGA)
.\testar-casos-de-uso.ps1 15     # Testa apenas o caso 15 (Fluxo de Caixa)
```

#### Linux/Mac (Bash):
```bash
cd docs/scripts
./testar-casos-de-uso.sh         # Testa todos os 18 casos
./testar-casos-de-uso.sh 5       # Testa apenas o caso 5 (SAGA)
./testar-casos-de-uso.sh 15      # Testa apenas o caso 15 (Fluxo de Caixa)
```

### Via curl (Exemplo: Caso 1 - Pedido Normal)

```bash
curl -X POST http://localhost:5000/api/pedidos \
  -H "Content-Type: application/json" \
  -d '{
    "clienteId": "CLI001",
    "restauranteId": "REST001",
    "itens": [
      {
        "produtoId": "PROD001",
        "nome": "Pizza Margherita",
        "quantidade": 1,
        "precoUnitario": 45.90
      }
    ],
    "enderecoEntrega": "Rua das Flores, 123",
    "formaPagamento": "CREDITO"
  }'
```

**Resposta esperada**:
```json
{
  "pedidoId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "mensagem": "Pedido recebido e está sendo processado.",
  "status": "Pendente"
}
```

### Via curl (Exemplo: Caso 13 - Lançamento Crédito)

```bash
curl -X POST http://localhost:5100/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": "Credito",
    "valor": 150.00,
    "descricao": "Venda de produto",
    "comerciante": "COM001",
    "categoria": "Vendas"
  }'
```

**Resposta esperada**:
```json
{
  "id": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "mensagem": "Lançamento registrado com sucesso.",
  "status": "Pendente"
}
```

### Via curl (Exemplo: Caso 15 - Consultar Consolidado)

```bash
curl -X GET "http://localhost:5100/api/consolidado?comerciante=COM001&data=2026-01-17"
```

**Resposta esperada**:
```json
{
  "comerciante": "COM001",
  "data": "2026-01-17",
  "totalCreditos": 150.00,
  "totalDebitos": 0.00,
  "saldoDiario": 150.00,
  "quantidadeCreditos": 1,
  "quantidadeDebitos": 0,
  "cacheHit": true
}
```

### Observando os Logs

Cada serviço gera logs estruturados com Serilog. Exemplo de fluxo completo:

```
[INFO] Validando pedido no restaurante REST001 com 1 itens
[INFO] Pedido validado. ValorTotal: R$ 45,90, TempoPreparo: 10min
[INFO] Processando pagamento. ClienteId: CLI001, Valor: R$ 45,90
[INFO] Pagamento aprovado. TransacaoId: TXN_abc123
[INFO] Alocando entregador. RestauranteId: REST001
[INFO] Entregador ENT001 alocado. TempoEstimado: 25min
[INFO] Notificação enviada com sucesso. Tipo: PedidoConfirmado
[INFO] SAGA finalizada com sucesso. Estado: PedidoConfirmado
```

---

## Documentação Completa

### Documentos Principais

- **[casos-uso.md](casos-uso.md)** - Detalhamento completo dos 12 cenários com payloads
- **[PLANO-EXECUCAO.md](PLANO-EXECUCAO.md)** - Plano de execução do projeto
- **[arquitetura.md](arquitetura.md)** - Detalhes da arquitetura e decisões técnicas
- **[guia-rebus.md](guia-rebus.md)** - Guia de uso do Rebus
- **[MIGRACAO-MASSTRANSIT-PARA-REBUS.md](MIGRACAO-MASSTRANSIT-PARA-REBUS.md)** - Documento da migração de MassTransit para Rebus

### Documentação Operacional (Fase 14)

- **[diagramas-compensacao.md](diagramas-compensacao.md)** - Diagramas detalhados dos fluxos de compensação e estados da SAGA
- **[runbook-troubleshooting.md](runbook-troubleshooting.md)** - Guia de diagnóstico e resolução de problemas comuns
- **[boas-praticas.md](boas-praticas.md)** - Guia de boas práticas para implementação e operação de SAGAs

### Scripts de Teste

- **[docs/scripts/readme-script.md](scripts/readme-script.md)** - Como usar os scripts de teste

### Documentação por Contexto

Este repositório implementa dois contextos principais:

#### 1. SAGA Pattern com Delivery de Comida

Demonstração educacional do padrão SAGA Orquestrado para sistemas distribuídos.

**Objetivo**: Mostrar implementação de SAGA com compensações e resiliência.

**Documentação**:
- [Arquitetura SAGA](arquitetura.md) - Detalhes técnicos da implementação
- [Casos de Uso](casos-uso.md) - 12 cenários implementados
- [Diagramas de Compensação](diagramas-compensacao.md) - Fluxos visuais
- [Boas Práticas](boas-praticas.md) - Os 10 mandamentos da SAGA
- [Runbook Troubleshooting](runbook-troubleshooting.md) - Guia operacional

**Serviços**: API, Orquestrador, Restaurante, Pagamento, Entregador, Notificação

#### 2. Fluxo de Caixa (CQRS + Event-Driven)

Sistema de controle de lançamentos com consolidado diário, demonstrando CQRS e arquitetura orientada a eventos.

**Objetivo**: Implementar sistema de alta performance com separação de responsabilidades.

**Documentação**:
- **[Fluxo de Caixa](fluxo-caixa.md)** - Documentação completa do contexto
- **[Diagramas Mermaid](diagramas-fluxo-caixa.md)** - Diagramas de arquitetura
- **[ADRs](decisoes-arquiteturais/)** - Decisões arquiteturais documentadas

**Características**:
- CQRS: Write Model (Lançamentos) e Read Model (Consolidado)
- Cache em 3 camadas (Memory + Redis + HTTP Response)
- Performance: 50+ req/s com latência P95 < 10ms
- Event-Driven: Sincronização assíncrona via RabbitMQ
- Observabilidade: Serilog + SEQ + Jaeger (OpenTelemetry)

**Serviços**: API, Lançamentos, Consolidado

**NFRs Atendidos**:
- Disponibilidade independente entre Lançamentos e Consolidado
- 50 req/s no Consolidado com < 5% de perda
- Escalabilidade horizontal
- Resiliência com retry automático

---

## Tecnologias Utilizadas

![Tech Stack](./images/tech-stack.png)  

---

## Conceitos Demonstrados

### 1. SAGA Orquestrado
- Saga centralizada com Rebus
- Controle de fluxo via Message Handlers
- Persistência do estado (InMemory para POC)
- Veja **[Diagramas de Compensação](diagramas-compensacao.md)** para detalhes visuais

### 2. Compensações Explícitas
- Rollback em ordem reversa
- Compensações implementadas manualmente nos handlers
- Idempotência (executar 2x não causa problema)
- Tratamento de erros estruturado
- Consulte **[Boas Práticas](boas-praticas.md)** para implementação correta

### 3. Result Pattern
- Encapsulamento de sucesso/falha
- Sem exceções para controle de fluxo
- Propagação de erros estruturados

### 4. Mensageria Assíncrona
- Request/Response via Rebus (Send/Reply)
- Roteamento baseado em tipos
- Dead Letter Queue automática

### Aprenda Mais

Para entender como implementar corretamente cada conceito, consulte:
- **[Boas Práticas](boas-praticas.md)** - Os 10 mandamentos da SAGA, com exemplos de código
- **[Diagramas de Compensação](diagramas-compensacao.md)** - Visualização completa dos fluxos

---

## Observabilidade

### Stack Completa Implementada (Fase 12)

A POC inclui observabilidade completa com **Serilog**, **SEQ** e **Jaeger**.

#### 1. **Logs Estruturados (Serilog + SEQ)**

- **URL SEQ**: http://localhost:5341 (admin/admin123)
- Logs estruturados em tempo real com queries poderosas
- Correlação por **CorrelationId** (rastreamento end-to-end)
- Visualização de:
  - **Transições de estado** da SAGA
  - **Compensações executadas**
  - **Eventos de domínio** e **Comandos**

**Exemplo de query no SEQ:**
```sql
Application = "SagaPoc.Orquestrador" AND CorrelationId = "a1b2c3d4-e5f6-7890"
```

#### 2. **Distributed Tracing (Jaeger + OpenTelemetry)**

- **URL**: http://localhost:16686
- Rastreamento end-to-end de todas as requisições
- Propagação de contexto através do RabbitMQ
- Visualização de latências por serviço
- Spans customizados para operações críticas

**Exemplo de uso:**
1. Acesse o Jaeger UI
2. Selecione o serviço `SagaPoc.Api`
3. Visualize o trace completo da SAGA
4. Identifique bottlenecks e falhas

#### 3. **RabbitMQ Management**

- **URL**: http://localhost:15672 (saga/saga123)
- Visualize filas, mensagens e consumers
- Monitore throughput e performance
- Gerencie exchanges e bindings

### Instrumentação Implementada

**OpenTelemetry (Jaeger):**
- **AspNetCore Instrumentation** - Traces HTTP automáticos
- **HttpClient Instrumentation** - Traces de chamadas externas
- **EntityFramework Instrumentation** - Traces de queries SQL
- **Rebus Integration** - Propagação de contexto via mensageria
- **Custom Spans** - Para operações de negócio críticas

**Serilog (SEQ):**
- **Enrichers** - Machine name, environment, thread, process
- **Structured Logging** - Properties extraíveis para queries
- **Correlation** - CorrelationId em todos os logs
- **Contexto** - Application, Service, Environment

### Troubleshooting

Para diagnosticar e resolver problemas comuns, consulte o **[Runbook de Troubleshooting](runbook-troubleshooting.md)** que cobre:
- SAGA travada
- Mensagens em Dead Letter Queue
- Compensação falhou
- Alta latência nas SAGAs
- Circuit breaker aberto
- Perda de mensagens
- Duplicação de pedidos

E muitos outros cenários com diagnóstico passo a passo e ações corretivas.

---

## Próximos Passos (Para Produção)

Esta POC é **educacional**. Para produção, considere:

### 1. Persistência da SAGA
- Trocar `InMemoryRepository` por **Postgres**
- Garantir recuperação em caso de reinicialização

### 2. Outbox Pattern
- Garantir atomicidade entre banco de dados e mensagens
- Evitar perda de mensagens

### 3. Retry Policy e Circuit Breaker
- Configurar retry exponencial
- Proteger serviços downstream

### 4. Idempotência
- Deduplicação de mensagens por MessageId
- Armazenamento em Redis/SQL

### 5. Observabilidade **Implementado (Fase 12)**
- Serilog + SEQ (Logs Estruturados)
- OpenTelemetry + Jaeger (Distributed Tracing)
- ⏳ Application Insights (Azure - opcional)

### 6. Testes
- Testes de integração automatizados
- Testes de carga (NBomber)
- Chaos Engineering

Veja mais detalhes em [plano-execucao.md - Seção 9](plano-execucao.md).

---

## Licença

Este projeto é licenciado sob a [MIT License](LICENSE).

---

## Contribuindo

Contribuições são bem-vindas! Sinta-se à vontade para:
- Reportar bugs
- Sugerir melhorias
- Adicionar novos casos de uso
- Melhorar a documentação

---

## Contato

Criado como material educacional sobre padrões de microsserviços.

---

## Agradecimentos

- [Rebus](https://github.com/rebus-org/Rebus) - Framework de mensageria simples e poderoso
- [RabbitMQ](https://www.rabbitmq.com/) - Message broker open source confiável e battle-tested
- [Chris Richardson](https://microservices.io/patterns/data/saga.html) - Padrão SAGA
- [Docker](https://www.docker.com/) - Containerização e simplificação de deploy

**Última atualização**: 2026-01-17 - Fluxo de Caixa (CQRS) integrado com 18 cenários de teste