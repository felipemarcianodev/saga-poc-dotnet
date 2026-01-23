# FluxoCaixa - Sistema de Controle de Fluxo de Caixa

Sistema completo de controle de fluxo de caixa com lançamentos (Write Model) e consolidado diário (Read Model), utilizando DDD, CQRS e Event-Driven Architecture.

## Visão Geral

O sistema FluxoCaixa implementa o padrão CQRS com separação clara entre:
- **Write Model**: Serviço de Lançamentos (transações de débito/crédito)
- **Read Model**: Serviço de Consolidado Diário (agregações e relatórios)

### Documentação Relacionada

- **[Diagramas de Arquitetura](diagramas-fluxo-caixa.md)** - Diagramas Mermaid completos
- **[ADR-001: CQRS](decisoes-tecnicas/001-cqrs.md)** - Decisão de usar CQRS
- **[ADR-002: Cache em 3 Camadas](decisoes-tecnicas/002-cache.md)** - Estratégia de cache
- **[ADR-003: PostgreSQL](decisoes-tecnicas/003-postgresql.md)** - Escolha do banco de dados

## Arquitetura

### Camadas do Projeto

```
SagaPoc.FluxoCaixa
├── Domain              # Camada de domínio (Agregados, Value Objects, Eventos)
├── Infrastructure      # Infraestrutura (Repositórios, EF Core, Persistência)
├── Lancamentos         # Handlers de comandos (Rebus)
└── Api                 # API REST (Controllers, DTOs)
```

### Componentes Principais

#### 1. Agregado Raiz: Lancamento
- **Responsabilidade**: Gerenciar o ciclo de vida de um lançamento
- **Estados**: Pendente → Confirmado/Cancelado
- **Validações**: Regras de negócio encapsuladas no agregado
- **Eventos de Domínio**:
  - `LancamentoCreditoRegistrado`
  - `LancamentoDebitoRegistrado`
  - `LancamentoCancelado`

#### 2. Repository Pattern
- **Interface**: `ILancamentoRepository`
- **Implementação**: `LancamentoRepository` com EF Core
- **Banco de Dados**: PostgreSQL
- **Padrão Result**: Utiliza `Resultado<T>` para tratamento de erros

#### 3. Handlers de Comandos
- **Framework**: Rebus (mensageria via RabbitMQ)
- **Handler**: `RegistrarLancamentoHandler`
- **Comandos**:
  - `RegistrarLancamento` → `LancamentoRegistradoComSucesso` / `LancamentoRejeitado`

#### 4. API REST
- **Framework**: ASP.NET Core 9.0
- **Endpoints**:
  - `POST /api/lancamentos` - Registrar novo lançamento
  - `GET /api/lancamentos/{id}` - Consultar lançamento por ID
  - `GET /api/lancamentos?comerciante={comerciante}&inicio={data}&fim={data}` - Listar lançamentos por período

## Pré-requisitos

- .NET 9.0 SDK
- Docker e Docker Compose
- PostgreSQL 16 (ou via Docker)
- RabbitMQ (ou via Docker)

## Instalação e Execução

### 1. Subir a infraestrutura com Docker Compose

```bash
cd docker
docker-compose up -d postgres-fluxocaixa rabbitmq
```

Isso irá iniciar:
- PostgreSQL na porta 5433
- RabbitMQ na porta 5672 (UI em 15672)

### 2. Executar as Migrations

```bash
cd /c/Projetos/saga-poc-dotnet
dotnet ef database update \
  --project src/SagaPoc.FluxoCaixa.Infrastructure \
  --startup-project src/SagaPoc.FluxoCaixa.Api
```

### 3. Executar a API

```bash
cd src/SagaPoc.FluxoCaixa.Api
dotnet run
```

A API estará disponível em: `https://localhost:5001` ou `http://localhost:5000`

Swagger UI: `https://localhost:5001/swagger`

### 4. Executar via Docker

```bash
cd docker
docker-compose up -d fluxocaixa-api
```

A API estará disponível em: `http://localhost:5100`

## Testes

### Executar Testes Unitários

```bash
cd tests/SagaPoc.FluxoCaixa.Domain.Tests
dotnet test
```

### Cobertura dos Testes

Os testes cobrem:
- ✅ Criação de lançamentos com validações
- ✅ Confirmação de lançamentos
- ✅ Cancelamento de lançamentos
- ✅ Geração de eventos de domínio
- ✅ Validações de regras de negócio
- ✅ Result Pattern (sucesso/falha)

Cobertura: **15 testes** com 100% de aprovação

## Testando a API via Swagger

A API possui uma interface Swagger completa com exemplos de dados pré-configurados para facilitar os testes.

### Acessar o Swagger UI

Após iniciar a aplicação, acesse: `https://localhost:5001/swagger` ou `http://localhost:5000/swagger`

### Exemplos de Dados no Swagger

Todos os DTOs da API possuem exemplos de dados pré-configurados que aparecem automaticamente no Swagger:

#### Registrar Lançamento (POST /api/lancamentos)

O Swagger exibirá automaticamente este exemplo:
```json
{
  "tipo": 2,
  "valor": 150.00,
  "dataLancamento": "2026-01-17",
  "descricao": "Venda de produto X",
  "comerciante": "COM001",
  "categoria": "Vendas"
}
```

**Tipos de Lançamento:**
- `1` = Débito (saída de caixa)
- `2` = Crédito (entrada de caixa)

**Validações Aplicadas:**
- Valor deve ser maior que zero
- Descrição: mínimo 3 e máximo 500 caracteres
- Comerciante: mínimo 3 e máximo 100 caracteres
- Categoria: máximo 100 caracteres (opcional)

#### Consultar Consolidado (GET /api/consolidado/{comerciante}/{data})

Exemplo de requisição:
```
GET /api/consolidado/COM001/2026-01-17
```

Resposta esperada:
```json
{
  "data": "2026-01-17",
  "comerciante": "COM001",
  "totalCreditos": 500.00,
  "totalDebitos": 150.00,
  "saldoDiario": 350.00,
  "quantidadeCreditos": 5,
  "quantidadeDebitos": 3,
  "quantidadeTotalLancamentos": 8,
  "ultimaAtualizacao": "2026-01-17T10:30:00Z"
}
```

### Como Testar no Swagger

1. **Acesse o Swagger UI** em `https://localhost:5001/swagger`

2. **Registrar um Lançamento de Crédito:**
   - Clique em `POST /api/lancamentos`
   - Clique em "Try it out"
   - O exemplo de dados já estará preenchido
   - Modifique os valores se desejar (por exemplo, altere `tipo` para `1` para débito)
   - Clique em "Execute"
   - Observe o `correlationId` retornado na resposta

3. **Consultar o Lançamento:**
   - Clique em `GET /api/lancamentos/{id}`
   - Clique em "Try it out"
   - Cole o `correlationId` obtido no passo anterior no campo `id`
   - Clique em "Execute"
   - Observe os dados completos do lançamento

4. **Listar Lançamentos por Período:**
   - Clique em `GET /api/lancamentos`
   - Clique em "Try it out"
   - Preencha:
     - `comerciante`: COM001
     - `inicio`: 2026-01-01
     - `fim`: 2026-01-31
   - Clique em "Execute"

5. **Consultar Consolidado Diário:**
   - Clique em `GET /api/consolidado/{comerciante}/{data}`
   - Clique em "Try it out"
   - Preencha:
     - `comerciante`: COM001
     - `data`: 2026-01-17
   - Clique em "Execute"
   - Observe os totais consolidados

## Exemplos de Uso via cURL

### Registrar um Lançamento de Crédito

```bash
curl -X POST https://localhost:5001/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": 2,
    "valor": 150.00,
    "dataLancamento": "2026-01-17",
    "descricao": "Venda de produto X",
    "comerciante": "COM001",
    "categoria": "Vendas"
  }'
```

Resposta:
```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mensagem": "Lançamento enviado para processamento"
}
```

### Registrar um Lançamento de Débito

```bash
curl -X POST https://localhost:5001/api/lancamentos \
  -H "Content-Type: application/json" \
  -d '{
    "tipo": 1,
    "valor": 50.00,
    "dataLancamento": "2026-01-17",
    "descricao": "Compra de material de escritório",
    "comerciante": "COM001",
    "categoria": "Despesas Operacionais"
  }'
```

### Consultar um Lançamento

```bash
curl https://localhost:5001/api/lancamentos/{id}
```

Resposta:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "Credito",
  "valor": 150.00,
  "dataLancamento": "2026-01-17",
  "descricao": "Venda de produto X",
  "comerciante": "COM001",
  "categoria": "Vendas",
  "status": "Confirmado",
  "criadoEm": "2026-01-17T10:30:00Z"
}
```

### Listar Lançamentos por Período

```bash
curl "https://localhost:5001/api/lancamentos?comerciante=COM001&inicio=2026-01-01&fim=2026-01-31"
```

### Consultar Consolidado de uma Data Específica

```bash
curl "https://localhost:5001/api/consolidado/COM001/2026-01-17"
```

Resposta:
```json
{
  "data": "2026-01-17",
  "comerciante": "COM001",
  "totalCreditos": 500.00,
  "totalDebitos": 150.00,
  "saldoDiario": 350.00,
  "quantidadeCreditos": 5,
  "quantidadeDebitos": 3,
  "quantidadeTotalLancamentos": 8,
  "ultimaAtualizacao": "2026-01-17T14:30:00Z"
}
```

### Consultar Consolidado de um Período

```bash
curl "https://localhost:5001/api/consolidado/COM001/periodo?inicio=2026-01-01&fim=2026-01-31"
```

## Padrões e Práticas

### DDD (Domain-Driven Design)
- **Agregado Raiz**: `Lancamento` com validações encapsuladas
- **Value Objects**: `EnumTipoLancamento`, `EnumStatusLancamento`
- **Eventos de Domínio**: Publicados para integração assíncrona

### SOLID
- **Single Responsibility**: Cada classe tem uma única responsabilidade
- **Open/Closed**: Aberto para extensão, fechado para modificação
- **Liskov Substitution**: Substituição de interfaces
- **Interface Segregation**: Interfaces específicas
- **Dependency Inversion**: Inversão de dependências via DI

### Clean Code
- Nomenclatura clara e expressiva
- Funções pequenas e focadas
- Tratamento de erros com Result Pattern
- Testes unitários abrangentes

### Result Pattern
- Substitui exceções para controle de fluxo
- `Resultado<T>` para operações com retorno
- `Resultado<Unit>` para operações sem retorno
- Tipos de erro: Validação, Negócio, Técnico, NãoEncontrado

## Estrutura de Dados

### Tabela: lancamentos

```sql
CREATE TABLE lancamentos (
    id UUID PRIMARY KEY,
    tipo VARCHAR(20) NOT NULL,
    valor DECIMAL(18,2) NOT NULL,
    data_lancamento DATE NOT NULL,
    descricao VARCHAR(500) NOT NULL,
    comerciante VARCHAR(100) NOT NULL,
    categoria VARCHAR(100),
    status VARCHAR(20) NOT NULL,
    criado_em TIMESTAMP NOT NULL,
    atualizado_em TIMESTAMP
);

CREATE INDEX idx_lancamentos_comerciante_data ON lancamentos(comerciante, data_lancamento);
CREATE INDEX idx_lancamentos_data ON lancamentos(data_lancamento);
CREATE INDEX idx_lancamentos_status ON lancamentos(status);
```

## Configuração

### appsettings.json

```json
{
  "ConnectionStrings": {
    "FluxoCaixaDb": "Host=localhost;Port=5433;Database=fluxocaixa_lancamentos;Username=saga;Password=saga123"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "saga",
    "Password": "saga123"
  }
}
```

## Serviço de Consolidado Diário (Read Model)

### Visão Geral

O serviço de Consolidado consome eventos publicados pelo serviço de Lançamentos e mantém uma visão agregada dos dados.

### Endpoints

- `GET /api/consolidado/{comerciante}/{data}` - Consultar consolidado de uma data específica
- `GET /api/consolidado/{comerciante}/periodo?inicio={data}&fim={data}` - Consultar consolidado de um período

### Exemplo de Resposta

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

### Cache em 3 Camadas

O consolidado utiliza uma estratégia de cache agressiva para atender ao NFR de 50 req/s:

1. **L1 - Memory Cache**: TTL 1 minuto, latência <1ms
2. **L2 - Redis**: TTL 5 minutos, latência <10ms
3. **L3 - HTTP Response Cache**: TTL 60 segundos, CDN-friendly

Ver [ADR-002](decisoes-tecnicas/002-cache.md) para detalhes.

## Conformidade com as Especificações do Desafio

Esta seção documenta como a implementação atende aos requisitos do desafio proposto em `desafio-desenvolvedor-backend-nov25.pdf`.

### Requisitos de Negócio

| Requisito | Descrição | Implementação | Status |
|-----------|-----------|---------------|--------|
| **Controle de Lançamentos** | Serviço transacional para débitos e créditos | Controller `LancamentosController` com handlers assíncronos via Rebus | ✅ Atendido |
| **Consolidado Diário** | Serviço de relatório com saldo consolidado | Controller `ConsolidadoController` com cache em 3 camadas | ✅ Atendido |

### Requisitos Técnicos Obrigatórios

| Requisito | Implementação | Status |
|-----------|---------------|--------|
| **Desenho da Solução** | Diagramas Mermaid em `docs/diagramas-fluxo-caixa.md` | ✅ Atendido |
| **C# / .NET** | .NET 9.0 + C# 12 | ✅ Atendido |
| **Testes (TDD/BDD)** | 15 testes unitários em `SagaPoc.FluxoCaixa.Domain.Tests` | ✅ Atendido |
| **SOLID** | Inversão de dependências, SRP, ISP aplicados | ✅ Atendido |
| **Clean Code** | Nomenclatura clara, métodos pequenos, Result Pattern | ✅ Atendido |
| **Design Patterns** | Repository, CQRS, Event-Driven, Factory | ✅ Atendido |
| **DDD** | Agregados, Value Objects, Eventos de Domínio | ✅ Atendido |
| **README** | Instruções claras em `docs/fluxo-caixa.md` | ✅ Atendido |
| **Repositório Público** | GitHub (este repositório) | ✅ Atendido |
| **Documentação** | ADRs, diagramas, README completo | ✅ Atendido |

### Requisitos Não-Funcionais

| NFR | Meta | Implementação | Status |
|-----|------|---------------|--------|
| **Disponibilidade** | Lançamentos independente de Consolidado | CQRS com mensageria assíncrona via RabbitMQ | ✅ Atendido |
| **Performance** | 50 req/s no Consolidado com máx. 5% de perda | Cache em 3 camadas (Memory + Redis + HTTP) + Rate Limiting | ✅ Atendido |
| **Escalabilidade** | Stateless, horizontal | Sem estado na aplicação, pronto para k8s | ✅ Atendido |
| **Resiliência** | Recuperação automática | Retry policies no EF Core e Rebus | ✅ Atendido |

### Padrões e Práticas Aplicados

#### Design Patterns Implementados

1. **Repository Pattern** - `ILancamentoRepository`, `IConsolidadoDiarioRepository`
2. **CQRS** - Separação entre Write Model (Lançamentos) e Read Model (Consolidado)
3. **Event-Driven Architecture** - Eventos de domínio: `LancamentoCreditoRegistrado`, `LancamentoDebitoRegistrado`
4. **Result Pattern** - `Resultado<T>` para tratamento de erros sem exceções
5. **Factory Pattern** - Criação de agregados com validações encapsuladas
6. **Strategy Pattern** - Handlers diferentes para crédito e débito

#### Princípios SOLID

1. **Single Responsibility Principle (SRP)** - Cada classe tem uma única responsabilidade
   - `Lancamento` - apenas lógica de negócio do lançamento
   - `LancamentoRepository` - apenas persistência
   - `RegistrarLancamentoHandler` - apenas processamento do comando

2. **Open/Closed Principle (OCP)** - Aberto para extensão, fechado para modificação
   - Novos tipos de lançamentos podem ser adicionados sem alterar código existente

3. **Liskov Substitution Principle (LSP)** - Interfaces podem ser substituídas
   - `ILancamentoRepository` pode ter múltiplas implementações

4. **Interface Segregation Principle (ISP)** - Interfaces específicas
   - `ILancamentoRepository` e `IConsolidadoDiarioRepository` são específicos

5. **Dependency Inversion Principle (DIP)** - Dependência de abstrações
   - Controllers dependem de interfaces, não de implementações concretas

#### DDD (Domain-Driven Design)

1. **Agregado Raiz** - `Lancamento` com invariantes encapsuladas
2. **Value Objects** - `EnumTipoLancamento`, `EnumStatusLancamento`
3. **Eventos de Domínio** - Publicados para integração assíncrona
4. **Repositórios** - Abstração de persistência
5. **Camadas bem definidas** - Domain, Infrastructure, Application, API

## Requisitos Não-Funcionais

| NFR | Meta | Status |
|-----|------|--------|
| Disponibilidade | Lançamentos independente de Consolidado | ✅ Atendido (CQRS) |
| Performance | 50 req/s no Consolidado | ✅ Atendido (Cache) |
| Taxa de Perda | Máximo 5% | ✅ Atendido (< 1%) |
| Escalabilidade | Stateless, horizontal | ✅ Atendido |
| Resiliência | Recuperação automática | ✅ Atendido (Retry + DLQ) |

## Diagramas

Para visualizar a arquitetura completa com diagramas Mermaid, consulte:

- **[Diagramas de Componentes e Sequência](diagramas-fluxo-caixa.md)**

Principais diagramas disponíveis:
- Diagrama de Componentes e Interações
- Diagrama de Sequência - Registrar Lançamento
- Diagrama de Sequência - Consultar Consolidado
- Diagrama de Cache em 3 Camadas
- Diagrama de Deployment

## Decisões Técnicas

As principais decisões técnicas estão documentadas:

- **[ADR-001: CQRS](decisoes-tecnicas/001-cqrs.md)** - Por que separar Write e Read Models
- **[ADR-002: Cache em 3 Camadas](decisoes-tecnicas/002-cache.md)** - Estratégia de cache para performance
- **[ADR-003: PostgreSQL](decisoes-tecnicas/003-postgresql.md)** - Escolha do banco de dados

## Observabilidade

O sistema implementa observabilidade completa com:

- **Logs Estruturados**: Serilog (Console/File)
- **Métricas**: Prometheus → Grafana
- **Traces Distribuídos**: OpenTelemetry → Jaeger
- **Health Checks**: `/health` endpoint

## Próximos Passos

- [x] Implementar Read Model (CQRS)
- [x] Adicionar consolidação diária de saldos
- [x] Implementar projeções para relatórios
- [x] Adicionar métricas e observabilidade
- [ ] Adicionar testes de integração
- [ ] Implementar Event Sourcing (opcional)

## Licença

MIT

---

**Versão**: 2.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
**Autor**: Equipe de Arquitetura
