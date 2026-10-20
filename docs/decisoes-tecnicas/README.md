# Decisões técnicas (ADRs)

Registros de decisões de arquitetura (ADR) do contexto de Fluxo de Caixa. Cada ADR diz qual problema havia, o que foi decidido, o que se perdeu com isso e o que pode dar errado.

---

## ADRs

| ADR | Status | Decisão |
|---|---|---|
| [001](./001-cqrs.md) | Aceito | Escrita (lançamentos) e leitura (consolidado) em serviços e bancos separados, ligados por eventos no RabbitMQ, para que o registro não dependa do consolidado. |
| [002](./002-cache.md) | Aceito, com defeito conhecido | Cache em três camadas na consulta do consolidado (response cache, memória e Redis). A camada Redis hoje não serve leituras, e a leitura pode vir defasada até cerca de 60 s. A meta de 50 requisições por segundo é objetivo, não medido. |
| [003](./003-postgresql.md) | Aceito | PostgreSQL 16 para os dois modelos, em instâncias separadas, cada uma migrada pelo seu worker. |

As escolhas da SAGA de pedidos (Rebus, RabbitMQ, estado no PostgreSQL) estão em [design/escolhas-tecnicas.md](../design/escolhas-tecnicas.md).

---

## Como criar um ADR

1. Use o próximo número livre: `NNN-nome-da-decisao.md` nesta pasta.
2. Siga o modelo abaixo.
3. Acrescente uma linha na tabela acima.

Regras:
- Métrica de desempenho ou disponibilidade só com teste ou monitoramento que a comprove; sem isso, escreva como objetivo não medido.
- A seção "O que pode dar errado" é obrigatória e lista riscos reais, não genéricos.
- ADR não é apagado: quando uma decisão é substituída, o status passa a "Substituído por NNN".

```markdown
# ADR NNN: Título da decisão

## Status

**Proposto** | **Aceito** | **Rejeitado** | **Substituído por NNN**

## Contexto

O problema ou a necessidade que motivou a decisão.

### Alternativas consideradas

- Alternativa 1: a favor, contra.
- Alternativa 2: a favor, contra.

## Decisão

O que foi decidido e como está implementado.

## Consequências

### Positivas
- ...

### Negativas
- ...

## O que pode dar errado

- Risco real, com o que acontece e como se percebe.

## Referências

- ...

---

**Data:** AAAA-MM-DD
```

---

## Referências

- [Architecture decision record, Joel Parker Henderson](https://github.com/joelparkerhenderson/architecture-decision-record)
- [Documenting Architecture Decisions, Michael Nygard](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions)
