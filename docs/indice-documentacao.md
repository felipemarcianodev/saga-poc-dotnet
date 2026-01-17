# Índice da Documentação

Guia completo da documentação do repositório, organizando todos os documentos por categoria e contexto.

---

## Documentação Principal

### README Geral
- **[README.md](README.md)** - Visão geral do repositório com ambos os contextos

---

## Contexto 1: SAGA Pattern (Delivery de Comida)

### Arquitetura e Design
- **[Arquitetura SAGA](arquitetura.md)** - Detalhes técnicos completos da implementação SAGA
- **[Diagramas de Compensação](diagramas-compensacao.md)** - Fluxos visuais de compensação em cascata

### Operação e Uso
- **[Casos de Uso](casos-uso.md)** - 12 cenários implementados com exemplos
- **[Boas Práticas](boas-praticas.md)** - Os 10 mandamentos da SAGA
- **[Runbook Troubleshooting](runbook-troubleshooting.md)** - Guia operacional de diagnóstico e resolução

---

## Contexto 2: Fluxo de Caixa (CQRS + Event-Driven)

### Documentação Principal
- **[Fluxo de Caixa](fluxo-caixa.md)** - Documentação completa do contexto

### Arquitetura e Design
- **[Diagramas Mermaid](diagramas-fluxo-caixa.md)** - Diagramas de arquitetura, sequência e deployment
- **[ADRs - Decisões Arquiteturais](decisoes-arquiteturais/README.md)**
  - [ADR-001: CQRS](decisoes-arquiteturais/001-cqrs.md)
  - [ADR-002: Cache em 3 Camadas](decisoes-arquiteturais/002-cache.md)
  - [ADR-003: PostgreSQL](decisoes-arquiteturais/003-postgresql.md)

### API e Documentação
- **[Swagger/OpenAPI](swagger-openapi.md)** - Documentação da API REST

### Operação e Troubleshooting
- **[Troubleshooting FluxoCaixa](troubleshooting-fluxo-caixa.md)** - Guia específico de diagnóstico e resolução

---

## Plano de Execução

### Fases do Projeto
- **[Visão Geral](plano-execucao/visao-geral.md)** - Visão geral das fases

### Fases SAGA (1-22)
- [Fase 01 - Fundação Result Pattern](plano-execucao/fase-01-fundacao-result-pattern.md)
- [Fase 02 - Configuração Rebus RabbitMQ](plano-execucao/fase-02-configuracao-rebus-rabbitmq.md)
- [Fase 03 - Implementação SAGA Rebus](plano-execucao/fase-03-implementacao-saga-rebus.md)
- [Fase 04 - Implementação Serviços Consumers](plano-execucao/fase-04-implementacao-servicos-consumers.md)
- [Fase 05 - API REST Ponto de Entrada](plano-execucao/fase-05-api-rest-ponto-entrada.md)
- [Fase 06 - Casos de Uso e Cenários de Teste](plano-execucao/fase-06-casos-uso-cenarios-teste.md)
- [Fase 07 - Documentação Completa](plano-execucao/fase-07-documentacao-completa.md)
- [Fase 09 - Integração Result Pattern](plano-execucao/fase-09-integracao-result-pattern.md)
- [Fase 10 - Tratamentos Resiliência](plano-execucao/fase-10-tratamentos-resiliencia.md)
- [Fase 11 - Compensação Rollback](plano-execucao/fase-11-compensacao-rollback.md)
- [Fase 12 - Observabilidade OpenTelemetry](plano-execucao/fase-12-observabilidade-opentelemetry.md)
- [Fase 13 - Testes Cenários Complexos](plano-execucao/fase-13-testes-cenarios-complexos.md)
- [Fase 14 - Documentação Refinamento](plano-execucao/fase-14-documentacao-refinamento.md)
- [Fase 15 - Migração RabbitMQ](plano-execucao/fase-15-migracao-rabbitmq.md)
- [Fase 16 - Persistência Estado SAGA](plano-execucao/fase-16-persistencia-estado-saga.md)
- [Fase 17 - Outbox Pattern](plano-execucao/fase-17-outbox-pattern.md)
- [Fase 18 - Observabilidade OpenTelemetry Avançado](plano-execucao/fase-18-observabilidade-opentelemetry-avancado.md)
- [Fase 19 - Retry Circuit Breaker](plano-execucao/fase-19-retry-circuit-breaker.md)
- [Fase 20 - Idempotência Deduplicação](plano-execucao/fase-20-idempotencia-deduplicacao.md)
- [Fase 21 - MongoDB Auditoria](plano-execucao/fase-21-mongodb-auditoria.md)
- [Fase 22 - Segurança Autenticação](plano-execucao/fase-22-seguranca-autenticacao.md)

### Fases Fluxo de Caixa (23-27)
- [Fase 23 - Análise e Design Fluxo de Caixa](plano-execucao/fase-23-analise-design-fluxo-caixa.md)
- [Fase 24 - Implementação Serviço Lançamentos](plano-execucao/fase-24-implementacao-servico-lancamentos.md)
- [Fase 25 - Implementação Serviço Consolidado](plano-execucao/fase-25-implementacao-servico-consolidado.md)
- [Fase 26 - Testes TDD BDD](plano-execucao/fase-26-testes-tdd-bdd.md)
- **[Fase 27 - Diagramas e Documentação](plano-execucao/fase-27-diagramas-documentacao.md)** ✅ Concluída

---

## Scripts e Utilitários

### Scripts de Teste
- **[README Scripts](scripts/readme-script.md)** - Como usar os scripts de teste

---

## Geração de Imagens

- **[Gerador de Imagens](gerador-imagens.md)** - Scripts para gerar diagramas visuais
- **[Gerador de Imagens Fluxo de Caixa](gerador-imagens-fluxo-caixa.md)** - Scripts específicos para FluxoCaixa

---

## Estrutura de Pastas

```
docs/
├── README.md                           # README principal do repositório
├── INDICE-DOCUMENTACAO.md             # Este arquivo
│
├── arquitetura.md                      # Arquitetura do SAGA
├── boas-praticas.md                   # Boas práticas SAGA
├── casos-uso.md                       # 12 cenários SAGA
├── diagramas-compensacao.md           # Diagramas de compensação SAGA
├── runbook-troubleshooting.md         # Troubleshooting SAGA
│
├── fluxo-caixa.md                     # Documentação principal FluxoCaixa
├── diagramas-fluxo-caixa.md          # Diagramas Mermaid FluxoCaixa
├── swagger-openapi.md                 # Documentação API REST
├── troubleshooting-fluxo-caixa.md    # Troubleshooting FluxoCaixa
│
├── decisoes-arquiteturais/            # ADRs
│   ├── 001-cqrs.md
│   ├── 002-cache.md
│   └── 003-postgresql.md
│
├── plano-execucao/                    # Fases do projeto
│   ├── visao-geral.md
│   ├── fase-01-*.md
│   └── ... (27 fases)
│
├── scripts/                           # Scripts de teste
│   └── readme-script.md
│
├── images/                            # Diagramas visuais
│
└── fluxocaixa/                        # (Legado - movido para fluxo-caixa.md)
    └── README.md
```

---

## Navegação Rápida

### Por Tópico

#### Arquitetura
- [Arquitetura SAGA](arquitetura.md)
- [Diagramas Fluxo de Caixa](diagramas-fluxo-caixa.md)
- [ADR-001: CQRS](decisoes-arquiteturais/001-cqrs.md)
- [ADR-002: Cache](decisoes-arquiteturais/002-cache.md)
- [ADR-003: PostgreSQL](decisoes-arquiteturais/003-postgresql.md)

#### Operação
- [Troubleshooting SAGA](runbook-troubleshooting.md)
- [Troubleshooting FluxoCaixa](troubleshooting-fluxo-caixa.md)
- [Boas Práticas SAGA](boas-praticas.md)

#### Desenvolvimento
- [Casos de Uso SAGA](casos-uso.md)
- [Swagger/OpenAPI](swagger-openapi.md)
- [Scripts de Teste](scripts/readme-script.md)

#### Planejamento
- [Visão Geral das Fases](plano-execucao/visao-geral.md)
- [Fase 27 - Atual](plano-execucao/fase-27-diagramas-documentacao.md)

---

## Como Contribuir com a Documentação

### Adicionando Novo Documento

1. Crie o arquivo na pasta apropriada (`docs/` ou subpasta)
2. Siga o template de metadados no final:
   ```markdown
   ---
   **Versão**: X.X
   **Data de criação**: YYYY-MM-DD
   **Última atualização**: YYYY-MM-DD
   **Autor**: Seu Nome
   ```
3. Adicione referência neste índice
4. Atualize links em documentos relacionados

### Template de Documento

```markdown
# Título do Documento

Breve descrição do propósito do documento.

---

## Seção 1

Conteúdo...

## Seção 2

Conteúdo...

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
**Autor**: Seu Nome
```

---

## Changelog da Documentação

### 2026-01-15 - Fase 27 Concluída

**Adicionado**:
- [Diagramas Mermaid FluxoCaixa](diagramas-fluxo-caixa.md)
- [ADR-001: CQRS](decisoes-arquiteturais/001-cqrs.md)
- [ADR-002: Cache em 3 Camadas](decisoes-arquiteturais/002-cache.md)
- [ADR-003: PostgreSQL](decisoes-arquiteturais/003-postgresql.md)
- [Swagger/OpenAPI](swagger-openapi.md)
- [Troubleshooting FluxoCaixa](troubleshooting-fluxo-caixa.md)
- [Índice de Documentação](INDICE-DOCUMENTACAO.md) (este arquivo)

**Modificado**:
- [README.md](README.md) - Adicionada seção "Documentação por Contexto"
- [Fluxo de Caixa](fluxo-caixa.md) - Reorganizado e expandido
- Program.cs da API - Configuração melhorada do Swagger

**Reorganizado**:
- Movido `docs/fluxocaixa/README.md` → `docs/fluxo-caixa.md`

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Mantenedor**: Equipe de Arquitetura
