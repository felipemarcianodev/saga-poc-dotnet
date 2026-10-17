# Separacao de Servicos de Escrita e Leitura

## Status

**Aceito**

## Contexto

Precisavamos que o servico de lancamentos continuasse funcionando mesmo se o consolidado caisse. A solucao obvia era separar em dois servicos.

### O Problema Real

Num sistema monolitico:
- Se o relatorio trava, a tela de lancamentos tambem trava
- Query pesada de agregacao compete com inserts
- Um deploy quebra tudo de uma vez

### Opcoes Consideradas

1. **Monolito com banco unico**: Simples, mas um problema afeta tudo
2. **Microservicos com banco compartilhado**: Servicos separados, mas banco ainda e gargalo
3. **CQRS (separar escrita de leitura)**: Mais complexo, mas resolve o problema de disponibilidade

## O Que Decidimos

Separamos em dois servicos com bancos diferentes:
- **Lancamentos** (Write): Grava no PostgreSQL, publica evento
- **Consolidado** (Read): Escuta evento, atualiza sua view no outro PostgreSQL

```
[API] -> [Lancamentos] -> PostgreSQL A
                |
                v (evento via RabbitMQ)
         [Consolidado] -> PostgreSQL B + Redis (cache)
```

## O Que Ganhamos

1. **Disponibilidade separada**: Consolidado pode cair sem afetar lancamentos
2. **Cache agressivo**: Read Model pode cachear sem medo de invalidacao complexa
3. **Escalar separadamente**: Mais replicas do Consolidado se precisar

## O Que Perdemos

1. **Consistencia imediata**: Usuario cria lancamento, mas consolidado pode nao refletir imediatamente
2. **Complexidade**: Dois bancos, message broker, sincronizacao via eventos
3. **Debugging mais dificil**: Fluxo assincrono e mais dificil de rastrear

## O Que Pode Dar Errado

- Se o RabbitMQ cair, os eventos se perdem (nao implementamos outbox)
- Se o Consolidado ficar muito atrasado, usuario ve dados velhos
- Nao testamos o que acontece com 1000 eventos/segundo
- Nao ha mecanismo para reconciliar se os modelos dessincronizarem

## Mitigacoes (O Que Fizemos)

- **UI Feedback**: Mostrar "Processando..." apos criar lancamento
- **Observabilidade**: Logs e tracing para ver o que esta acontecendo
- **Retry**: Mensagens sao reprocessadas se falharem (at-least-once)

## Mitigacoes (O Que NAO Fizemos)

- Outbox pattern para garantir que evento e gravado com lancamento
- Job de reconciliacao para detectar divergencias
- Testes de carga para validar NFRs

## Objetivos (Nao Validados)

| Objetivo | Descricao |
|----------|-----------|
| Disponibilidade Lancamentos | Alta (sem single point of failure com o Consolidado) |
| Throughput Consolidado | Suportar carga de leitura sem afetar escrita |
| Latencia Consolidado | Baixa com cache (nao testado com carga real) |

**Nota**: Estes objetivos nao foram validados com testes de carga.

## Referencias

- [CQRS Pattern - Martin Fowler](https://martinfowler.com/bliki/CQRS.html)
- [Event-Driven Architecture](https://martinfowler.com/articles/201701-event-driven.html)

---

**Data**: 2026-01-15 (atualizado 2026-01-21)
**Autor**: Equipe de Arquitetura
