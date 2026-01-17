# FASE 23: Análise e Design - Sistema de Fluxo de Caixa

## Contexto

Esta fase marca o início da implementação de um **novo domínio** no projeto. O sistema de fluxo de caixa será implementado seguindo os mesmos princípios arquiteturais da POC SAGA (SOLID, DDD, Clean Code, Design Patterns), mas focado em requisitos específicos de controle financeiro.

## Objetivos

- Analisar requisitos de negócio do sistema de fluxo de caixa
- Aplicar DDD para modelagem do domínio financeiro
- Desenhar arquitetura de microsserviços com separação de responsabilidades
- Garantir atendimento aos requisitos não-funcionais (NFRs)
- Criar diagrama de componentes e interações

## Descrição da Solução

**Contexto de Negócio:**
Um comerciante precisa controlar o seu fluxo de caixa diário com os lançamentos (débitos e créditos) e necessita de um relatório que disponibilize o saldo diário consolidado.

### Requisitos de Negócio

1. **Serviço Transacional**: Controle de lançamentos (débitos e créditos)
2. **Serviço de Relatório**: Consolidado diário com saldo

### Requisitos Não-Funcionais (NFRs)

| NFR | Descrição | Meta |
|-----|-----------|------|
| **Disponibilidade** | O serviço de lançamentos não deve ficar indisponível se o consolidado cair | 99.9% uptime independente |
| **Performance** | Consolidado diário deve processar 50 req/s | Max 5% de perda |
| **Escalabilidade** | Arquitetura stateless para dimensionamento horizontal | N/A |
| **Resiliência** | Recuperação automática de falhas | Circuit breaker + retry |

### Requisitos Técnicos Obrigatórios

- C# (.NET 9.0)
- Testes TDD/BDD com cobertura da lógica de negócio
- SOLID, Clean Code, Design Patterns, DDD
- Diagrama de componentes e interações
- README com instruções claras de execução local
- Documentação completa no repositório

## Arquitetura Proposta

### Visão Geral

```
┌─────────────────────────────────────────────────────────────────┐
│                      SISTEMA DE FLUXO DE CAIXA                  │
└─────────────────────────────────────────────────────────────────┘

┌──────────────────┐         ┌──────────────────────────────────┐
│   API Gateway    │◄────────┤  Cliente (Comerciante)           │
│  (REST API)      │         └──────────────────────────────────┘
└────────┬─────────┘
         │
         │ HTTP
         │
    ┌────▼────────────────────────────────────────────────┐
    │                                                      │
    │              Message Bus (RabbitMQ)                 │
    │                                                      │
    └───┬─────────────────────────────────────────┬───────┘
        │                                         │
        │ Async                                   │ Async
        │                                         │
┌───────▼─────────────────┐          ┌───────────▼──────────────┐
│  Serviço de             │          │  Serviço de              │
│  Lançamentos            │          │  Consolidado Diário      │
│  (Write Model)          │          │  (Read Model)            │
│                         │          │                          │
│  • Registrar Débito     │          │  • Consolidar Diário     │
│  • Registrar Crédito    │          │  • Consultar Saldo       │
│  • Validar Lançamento   │          │  • Gerar Relatório       │
└─────────┬───────────────┘          └──────────┬───────────────┘
          │                                     │
          │ Write                               │ Read
          │                                     │
    ┌─────▼─────────────┐            ┌─────────▼─────────────┐
    │  PostgreSQL       │            │  PostgreSQL           │
    │  (Lançamentos)    │            │  (Consolidados)       │
    └───────────────────┘            └───────────────────────┘
```

### Padrões Aplicados

#### 1. **CQRS (Command Query Responsibility Segregation)**

**Motivação**: Separar responsabilidades de escrita (lançamentos) e leitura (consolidados)

- **Write Model (Command)**: `SagaPoc.FluxoCaixa.Lancamentos`
  - Alta taxa de escrita
  - Validações de negócio
  - Event sourcing

- **Read Model (Query)**: `SagaPoc.FluxoCaixa.Consolidado`
  - Otimizado para leitura
  - Dados desnormalizados
  - Cache agressivo

#### 2. **Event-Driven Architecture**

**Eventos de Domínio:**

```csharp
// Eventos publicados pelo Serviço de Lançamentos
public record LancamentoCreditoRegistrado(
    Guid LancamentoId,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante
);

public record LancamentoDebitoRegistrado(
    Guid LancamentoId,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante
);

// Evento consumido pelo Serviço de Consolidado
public record ConsolidacaoDiariaSolicitada(
    DateTime Data,
    string Comerciante
);
```

#### 3. **Repository Pattern + Unit of Work**

```csharp
public interface ILancamentoRepository
{
    Task<Result<Lancamento>> AdicionarAsync(Lancamento lancamento);
    Task<Result<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(DateTime inicio, DateTime fim);
}

public interface IConsolidadoDiarioRepository
{
    Task<Result<ConsolidadoDiario>> ObterPorDataAsync(DateTime data);
    Task<Result> AtualizarAsync(ConsolidadoDiario consolidado);
}
```

#### 4. **Domain-Driven Design (DDD)**

**Agregados:**

```csharp
namespace SagaPoc.FluxoCaixa.Domain.Agregados;

// Agregado: Lançamento
public class Lancamento : AggregateRoot
{
    public Guid Id { get; private set; }
    public EnumTipoLancamento Tipo { get; private set; } // Débito ou Crédito
    public decimal Valor { get; private set; }
    public DateTime DataLancamento { get; private set; }
    public string Descricao { get; private set; }
    public string Comerciante { get; private set; }
    public EnumStatusLancamento Status { get; private set; }

    private Lancamento() { } // EF Core

    public static Result<Lancamento> Criar(
        EnumTipoLancamento tipo,
        decimal valor,
        DateTime dataLancamento,
        string descricao,
        string comerciante)
    {
        // Validações de domínio
        if (valor <= 0)
            return Result<Lancamento>.Failure(new Erro("Lancamento.ValorInvalido",
                "O valor deve ser maior que zero"));

        if (string.IsNullOrWhiteSpace(descricao))
            return Result<Lancamento>.Failure(new Erro("Lancamento.DescricaoObrigatoria",
                "A descrição é obrigatória"));

        var lancamento = new Lancamento
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Valor = valor,
            DataLancamento = dataLancamento,
            Descricao = descricao,
            Comerciante = comerciante,
            Status = EnumStatusLancamento.Pendente
        };

        lancamento.AdicionarEvento(tipo == EnumTipoLancamento.Credito
            ? new LancamentoCreditoRegistrado(lancamento.Id, valor, dataLancamento, descricao, comerciante)
            : new LancamentoDebitoRegistrado(lancamento.Id, valor, dataLancamento, descricao, comerciante));

        return Result<Lancamento>.Success(lancamento);
    }

    public Result Confirmar()
    {
        if (Status == EnumStatusLancamento.Confirmado)
            return Result.Failure(new Erro("Lancamento.JaConfirmado",
                "Lançamento já foi confirmado"));

        Status = EnumStatusLancamento.Confirmado;
        return Result.Success();
    }
}

// Value Objects
public enum EnumTipoLancamento { Debito, Credito }
public enum EnumStatusLancamento { Pendente, Confirmado, Cancelado }

// Agregado: ConsolidadoDiario
public class ConsolidadoDiario : AggregateRoot
{
    public Guid Id { get; private set; }
    public DateTime Data { get; private set; }
    public string Comerciante { get; private set; }
    public decimal TotalCreditos { get; private set; }
    public decimal TotalDebitos { get; private set; }
    public decimal SaldoDiario => TotalCreditos - TotalDebitos;
    public int QuantidadeLancamentos { get; private set; }
    public DateTime UltimaAtualizacao { get; private set; }

    private ConsolidadoDiario() { }

    public static ConsolidadoDiario Criar(DateTime data, string comerciante)
    {
        return new ConsolidadoDiario
        {
            Id = Guid.NewGuid(),
            Data = data.Date,
            Comerciante = comerciante,
            TotalCreditos = 0,
            TotalDebitos = 0,
            QuantidadeLancamentos = 0,
            UltimaAtualizacao = DateTime.UtcNow
        };
    }

    public void AplicarLancamento(EnumTipoLancamento tipo, decimal valor)
    {
        if (tipo == EnumTipoLancamento.Credito)
            TotalCreditos += valor;
        else
            TotalDebitos += valor;

        QuantidadeLancamentos++;
        UltimaAtualizacao = DateTime.UtcNow;
    }
}
```

## Design da Solução

### Componentes Principais

#### 1. **SagaPoc.FluxoCaixa.Api**
- Endpoints REST para lançamentos e consultas
- Validação de entrada
- Rate limiting (50 req/s)
- Autenticação/Autorização

#### 2. **SagaPoc.FluxoCaixa.Lancamentos (Write Model)**
- Processamento de comandos
- Validação de regras de negócio
- Publicação de eventos
- Persistência transacional

#### 3. **SagaPoc.FluxoCaixa.Consolidado (Read Model)**
- Consumo de eventos
- Consolidação diária
- Cache de relatórios
- Otimização para leitura

#### 4. **SagaPoc.FluxoCaixa.Domain**
- Agregados, Entidades, Value Objects
- Regras de domínio
- Eventos de domínio

#### 5. **SagaPoc.FluxoCaixa.Infrastructure**
- Repositórios
- Configurações EF Core
- Integração RabbitMQ

### Fluxo de Dados

```
┌─────────────────────────────────────────────────────────────────┐
│  FLUXO: Registrar Lançamento de Crédito                         │
└─────────────────────────────────────────────────────────────────┘

1. Cliente ──POST /api/lancamentos──> API Gateway
                                          │
                                          │ Validação
                                          │
2. API ──Publica RegistrarLancamentoCmd──> RabbitMQ
                                              │
                                              │
3. Serviço Lançamentos ◄──Consome────────────┘
        │
        │ Valida e Persiste
        │
4. Banco Write ◄──Insere Lançamento──────────┘
        │
        │ Transação OK
        │
5. Serviço Lançamentos ──Publica LancamentoCreditoRegistrado──> RabbitMQ
                                                                    │
                                                                    │
6. Serviço Consolidado ◄──Consome────────────────────────────────┘
        │
        │ Atualiza Consolidado
        │
7. Banco Read ◄──Atualiza Saldo Diário──────────┘

┌─────────────────────────────────────────────────────────────────┐
│  FLUXO: Consultar Saldo Consolidado                             │
└─────────────────────────────────────────────────────────────────┘

1. Cliente ──GET /api/consolidado/2026-01-15──> API Gateway
                                                    │
                                                    │ Cache?
                                                    │
2. API ──Consulta Banco Read──> PostgreSQL (Consolidados)
                                    │
                                    │
3. API ◄──Retorna Saldo─────────────┘
    │
    │ Cache por 5 minutos
    │
4. Cliente ◄──200 OK { saldo: 1500.00 }──────┘
```

## Estrutura de Pastas

```
src/
├── SagaPoc.FluxoCaixa.Api/
│   ├── Controllers/
│   │   ├── LancamentosController.cs
│   │   └── ConsolidadoController.cs
│   ├── DTOs/
│   │   ├── RegistrarLancamentoRequest.cs
│   │   └── ConsolidadoDiarioResponse.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── SagaPoc.FluxoCaixa.Lancamentos/
│   ├── Handlers/
│   │   ├── RegistrarLancamentoHandler.cs
│   │   └── ConfirmarLancamentoHandler.cs
│   ├── Servicos/
│   │   └── LancamentoServico.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── SagaPoc.FluxoCaixa.Consolidado/
│   ├── Handlers/
│   │   ├── LancamentoCreditoRegistradoHandler.cs
│   │   ├── LancamentoDebitoRegistradoHandler.cs
│   │   └── ConsolidarDiarioHandler.cs
│   ├── Servicos/
│   │   └── ConsolidacaoServico.cs
│   ├── Program.cs
│   └── appsettings.json
│
├── SagaPoc.FluxoCaixa.Domain/
│   ├── Agregados/
│   │   ├── Lancamento.cs
│   │   └── ConsolidadoDiario.cs
│   ├── Eventos/
│   │   ├── LancamentoCreditoRegistrado.cs
│   │   └── LancamentoDebitoRegistrado.cs
│   ├── Comandos/
│   │   ├── RegistrarLancamento.cs
│   │   └── ConsolidarDiario.cs
│   ├── Repositorios/
│   │   ├── ILancamentoRepository.cs
│   │   └── IConsolidadoDiarioRepository.cs
│   └── ValueObjects/
│       └── TipoLancamento.cs
│
└── SagaPoc.FluxoCaixa.Infrastructure/
    ├── Persistencia/
    │   ├── FluxoCaixaDbContext.cs
    │   ├── LancamentoRepository.cs
    │   └── ConsolidadoDiarioRepository.cs
    ├── Mensageria/
    │   └── RebusConfiguration.cs
    └── Migrations/
```

## Decisões Arquiteturais

### 1. CQRS para NFR de Disponibilidade

**Problema**: "O serviço de lançamentos não deve ficar indisponível se o consolidado cair"

**Solução**:
- Separação física de serviços (Write e Read)
- Comunicação assíncrona via eventos
- Bancos de dados separados
- Circuit breaker no consumidor de eventos

**Benefício**: Lançamentos continuam funcionando mesmo com consolidado offline

### 2. Event-Driven para Desacoplamento

**Problema**: Alta taxa de lançamentos não pode travar o consolidado

**Solução**:
- Lançamentos publicam eventos no RabbitMQ
- Consolidado consome de forma assíncrona
- Backpressure com filas persistentes
- Dead Letter Queue para erros

**Benefício**: 50 req/s processadas sem perda (< 5%)

### 3. PostgreSQL para Ambos os Modelos

**Motivação**:
- Transações ACID para lançamentos
- Índices otimizados para consultas de consolidado
- JSON para auditoria completa
- Backup e recovery simplificados

### 4. Cache em Múltiplas Camadas

**Problema**: 50 req/s no consolidado

**Solução**:
- Redis para cache distribuído
- Memory cache na API
- ETags para cache HTTP
- Invalidação baseada em eventos

## Métricas e Monitoramento

### KPIs de Negócio

| Métrica | Target | Alerta |
|---------|--------|--------|
| Lançamentos processados/min | > 3000 | < 2000 |
| Latência P95 lançamentos | < 100ms | > 200ms |
| Latência P95 consolidado | < 50ms | > 100ms |
| Taxa de erro | < 0.1% | > 1% |
| Disponibilidade lançamentos | 99.9% | < 99.5% |

### Observabilidade

- **Logs estruturados** (Serilog + SEQ)
- **Distributed tracing** (OpenTelemetry + Jaeger)
- **Health checks** customizados

## Diagramas

### Diagrama de Componentes

```
                    ┌─────────────────────────┐
                    │      API Gateway        │
                    │   (SagaPoc.FluxoCaixa   │
                    │        .Api)            │
                    └──────────┬──────────────┘
                               │
                ┌──────────────┴──────────────┐
                │                             │
        ┌───────▼────────┐           ┌───────▼────────┐
        │  RabbitMQ      │           │  Redis         │
        │  (Mensageria)  │           │  (Cache)       │
        └───────┬────────┘           └────────────────┘
                │
    ┌───────────┴───────────┐
    │                       │
┌───▼──────────┐    ┌──────▼────────┐
│  Serviço     │    │   Serviço     │
│  Lançamentos │    │  Consolidado  │
│  (Write)     │    │   (Read)      │
└───┬──────────┘    └──────┬────────┘
    │                      │
┌───▼──────────┐    ┌──────▼────────┐
│ PostgreSQL   │    │  PostgreSQL   │
│ (Lançamentos)│    │ (Consolidado) │
└──────────────┘    └───────────────┘
```

## Critérios de Aceitação

- [ ] Diagrama de componentes criado e documentado
- [ ] Modelo de domínio (DDD) definido com agregados e value objects
- [ ] Definição de eventos de domínio e comandos
- [ ] Arquitetura CQRS documentada
- [ ] Estratégia para atender NFRs documentada
- [ ] Estrutura de pastas criada
- [ ] Decisões arquiteturais registradas

## Trade-offs

**Benefícios:**
- Separação clara de responsabilidades (CQRS)
- Alta disponibilidade do serviço de lançamentos
- Performance otimizada para consultas
- Escalabilidade horizontal
- Aderência total aos princípios SOLID e DDD

**Considerações:**
- Complexidade adicional (2 bancos, eventos assíncronos)
- Eventual consistency entre lançamentos e consolidado
- Necessidade de sincronização inicial
- Infraestrutura mais complexa

## Estimativa

**Tempo Total**: 4-6 horas

- Análise de requisitos: 1 hora
- Modelagem DDD: 1-2 horas
- Diagrama de componentes: 1 hora
- Documentação de decisões: 1-2 horas
- Review arquitetural: 1 hora

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
