# Decisões Arquiteturais (ADRs)

Este diretório contém os Architectural Decision Records (ADRs) do sistema de Fluxo de Caixa.

---

## O que são ADRs?

ADRs (Architectural Decision Records) são documentos que capturam decisões arquiteturais importantes, incluindo:

- **Contexto**: Qual problema estamos resolvendo?
- **Decisão**: O que decidimos fazer?
- **Alternativas**: Que outras opções consideramos?
- **Consequências**: Quais são os trade-offs?

---

## ADRs Disponíveis

### ADR-001: CQRS para Separação de Lançamentos e Consolidado

**Status**: Aceito

**Resumo**: Uso do padrão CQRS para separar o Write Model (Lançamentos) do Read Model (Consolidado), garantindo disponibilidade independente e performance otimizada.

**Motivação**:
- Lançamentos não pode ficar indisponível se Consolidado cair
- Consolidado precisa suportar 50 req/s

**Link**: [001-cqrs.md](001-cqrs.md)

---

### ADR-002: Cache em 3 Camadas para Consolidado

**Status**: Aceito

**Resumo**: Implementação de cache em 3 camadas (Memory Cache + Redis + HTTP Response Cache) para atender ao NFR de 50 req/s com latência P95 < 10ms.

**Motivação**:
- PostgreSQL não aguenta 50 req/s sem cache
- Latência precisa ser < 100ms

**Link**: [002-cache.md](002-cache.md)

---

### ADR-003: PostgreSQL para Ambos os Modelos

**Status**: Aceito

**Resumo**: Uso de PostgreSQL tanto para Write Model quanto para Read Model, com esquemas e otimizações diferentes para cada.

**Motivação**:
- Simplicidade operacional (stack unificado)
- PostgreSQL é suficiente para ambos os casos de uso
- Reduz complexidade e custos

**Link**: [003-postgresql.md](003-postgresql.md)

---

## Timeline de Decisões

```
2026-01-15
│
├─ ADR-001: CQRS (Aceito)
│  └─ Separação de Write e Read Models
│
├─ ADR-002: Cache em 3 Camadas (Aceito)
│  └─ Memory Cache + Redis + HTTP Response Cache
│
└─ ADR-003: PostgreSQL (Aceito)
   └─ PostgreSQL para ambos os modelos
```

---

## Status dos ADRs

| ADR | Status | Data | Tópico |
|-----|--------|------|--------|
| ADR-001 | ✅ Aceito | 2026-01-15 | CQRS |
| ADR-002 | ✅ Aceito | 2026-01-15 | Cache em 3 Camadas |
| ADR-003 | ✅ Aceito | 2026-01-15 | PostgreSQL |

**Legenda**:
- ✅ Aceito: Decisão aprovada e implementada
- 🔄 Proposto: Em discussão
- ❌ Rejeitado: Decisão rejeitada
- ⚠️ Deprecated: Decisão substituída por outra

---

## Como Criar um Novo ADR

### 1. Numerar o ADR

Use o próximo número disponível (ex: 004)

### 2. Criar o Arquivo

```bash
touch docs/decisoes-tecnicas/004-nome-da-decisao.md
```

### 3. Usar o Template

```markdown
# ADR NNN: Título da Decisão

## Status

**Proposto** | **Aceito** | **Rejeitado** | **Deprecated**

## Contexto

Descrever o problema ou necessidade que motivou a decisão.

## Decisão

O que decidimos fazer?

## Alternativas Consideradas

### Alternativa 1
**Prós**: ...
**Contras**: ...

### Alternativa 2
**Prós**: ...
**Contras**: ...

## Consequências

### Positivas
- Benefício 1
- Benefício 2

### Negativas
- Trade-off 1
- Trade-off 2

## Referências

- Link 1
- Link 2

---

**Data**: YYYY-MM-DD
**Autor**: Nome
**Revisores**: Nome1, Nome2
**Status**: Aceito/Proposto/Rejeitado
```

### 4. Atualizar README

Adicione o novo ADR neste README.

---

## Princípios de ADRs

1. **Capturar Decisões Importantes**: Não documente decisões triviais
2. **Manter Histórico**: Nunca apague ADRs, apenas marque como Deprecated
3. **Ser Objetivo**: Foque em fatos e trade-offs, não em opiniões
4. **Incluir Contexto**: Futuras gerações precisam entender o "porquê"
5. **Documentar Alternativas**: Mostre que outras opções foram consideradas

---

## Referências

- [ADR GitHub - Joel Parker Henderson](https://github.com/joelparkerhenderson/architecture-decision-record)
- [Documenting Architecture Decisions - Michael Nygard](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
