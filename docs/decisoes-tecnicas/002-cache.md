# Estrategia de Cache para Consolidado

## Status

**Aceito**

## Contexto

O requisito dizia "50 req/s no consolidado". PostgreSQL sozinho provavelmente nao aguenta isso com queries de agregacao. Precisavamos de cache.

### O Problema

Cada consulta de consolidado faz:
- Query com SUM, COUNT no banco
- Join com possiveis centenas de lancamentos
- Agregacao por comerciante e data

Com 50 usuarios consultando ao mesmo tempo, o banco vai sofrer.

## O Que Decidimos

Cache em 3 camadas:

```
[Request]
    |
    v
[L1: Memory Cache] - TTL 1min, <1ms
    |
    v (miss)
[L2: Redis] - TTL 5min, ~5ms
    |
    v (miss)
[L3: PostgreSQL] - ~100ms
```

### Por Que 3 Camadas?

- **Memory Cache (L1)**: Mais rapido, mas nao compartilhado entre instancias
- **Redis (L2)**: Distribuido, compartilhado, mas com latencia de rede
- **PostgreSQL (L3)**: Source of truth, mais lento

A ideia e que a maioria dos requests bata no L1 ou L2.

## O Que Implementamos

```csharp
// L1: Memory Cache
services.AddMemoryCache();

// L2: Redis
services.AddStackExchangeRedisCache(options => {
    options.Configuration = "localhost:6379";
});

// L3: HTTP Response Cache (bonus)
[ResponseCache(Duration = 60)]
public async Task<IActionResult> ObterConsolidado(...)
```

## O Que NAO Implementamos

- **Invalidacao por evento**: Quando um lancamento e criado, o cache deveria ser invalidado. Nao fizemos. Dependemos do TTL.
- **Cache warm-up**: Nao pre-populamos o cache na inicializacao
- **Fallback se Redis cair**: Se o Redis morrer, requests vao direto pro banco. Nao testamos isso.
- **Metricas de hit rate**: Nao sabemos quanto % bate no cache vs banco

## O Que Pode Dar Errado

1. **Dados desatualizados**: Usuario cria lancamento, consulta consolidado, nao ve. Precisa esperar TTL expirar.
2. **Cache inconsistente**: Diferentes instancias podem ter dados diferentes no L1.
3. **Redis cai**: Sistema degrada pra query no banco. Pode nao aguentar a carga.
4. **Memoria estoura**: Se tiver muitos comerciantes/datas, L1 pode crescer demais.

## Trade-offs Aceitos

| O que aceitamos | Por que |
|-----------------|---------|
| Dados podem estar ate 5 min atrasados | Performance > consistencia imediata para relatorios |
| 3 camadas e complexo | Necessario para o NFR de 50 req/s (teoricamente) |
| Sem invalidacao por evento | Simplificou implementacao, TTL e "bom o suficiente" |

## Objetivos (Nao Validados)

| Objetivo | Esperado |
|----------|----------|
| Throughput | 50+ req/s |
| Latencia P95 | <50ms (com cache hit) |
| Hit Rate L1 | ~70-80% |
| Hit Rate L2 | ~15-20% |

**Nota**: Nao rodamos testes de carga. Esses numeros sao chutes educados baseados em sistemas similares.

## Se Tivessemos Mais Tempo

1. Invalidacao por evento (quando lancamento e criado, invalida cache daquela data/comerciante)
2. Metricas de hit rate no Prometheus
3. Fallback gracioso se Redis cair
4. Testes de carga reais com NBomber

## Referencias

- [Caching Strategies - Microsoft](https://docs.microsoft.com/azure/architecture/best-practices/caching)
- [Cache-Aside Pattern](https://docs.microsoft.com/azure/architecture/patterns/cache-aside)

---

**Data**: 2026-01-15 (atualizado 2026-01-21)
**Autor**: Equipe de Arquitetura
