# ADR 002: Cache na consulta do consolidado

## Status

**Aceito** (implementado, com defeito conhecido na camada Redis; ver "Estado atual")

## Contexto

O enunciado pedia que a consulta do consolidado suportasse 50 requisições por segundo. A consulta do dia já é barata (uma linha por dia e comerciante, com índice único; ver [ADR 003](./003-postgresql.md)), mas a ideia era tirar a maior parte das leituras do banco e manter a API de consulta funcionando com o banco lento.

## Decisão

Três camadas na frente do PostgreSQL, só em `GET /api/consolidado/{comerciante}/{data}`:

```
requisição
  -> response cache do ASP.NET Core   60 s   ([ResponseCache(Duration = 60)], por instância)
  -> IMemoryCache                       1 min  (por instância)
  -> Redis (IDistributedCache)          5 min  (compartilhado, prefixo "FluxoCaixa:")
  -> PostgreSQL fluxocaixa_consolidado
```

Chave: `consolidado:{comerciante}:{yyyy-MM-dd}`. No acerto do Redis, o valor também vai para a memória; na leitura do banco, vai para as duas.

**Invalidação:** a cada evento aplicado, o Consolidado apaga a chave do dia no Redis (mesmo Redis e mesmo prefixo da API). A memória e o response cache não são invalidados; expiram pelo tempo.

`GET /api/consolidado/{comerciante}/periodo` não usa cache.

Na frente de tudo, um limitador de janela fixa (`consolidado`): 50 requisições por segundo, fila de 10.

### Alternativas

- **Só o banco:** a consulta é uma busca por índice único; pode bastar, mas não foi medido.
- **Só Redis:** uma fonte compartilhada, invalidada pelo Consolidado, sem defasagem por instância; cada leitura paga uma ida à rede.
- **Memória com invalidação por mensagem:** o Consolidado publicaria um evento e cada instância da API limparia a própria memória; mais uma assinatura para manter.

## Estado atual

- **A camada Redis não funciona como leitura.** A API grava o `ConsolidadoDiario` no Redis em JSON, mas não consegue lê-lo de volta: o `System.Text.Json` não desserializa o agregado (construtor sem parâmetros privado e propriedades com `private set`). O `RedisCacheService` captura a exceção (`NotSupportedException`), registra `Erro ao buscar do cache` como aviso e devolve vazio, e a consulta cai no banco. Conferido em 2026-10-20 contra a stack local: a segunda leitura de uma chave presente no Redis gerou o aviso e foi ao banco. Na prática, o cache efetivo é só o response cache e a memória.
- O caminho de invalidação (o Consolidado apagando a chave) funciona, mas, pelo item anterior, não muda o que a API devolve.
- A correção é desserializar para um DTO de leitura (ou dar ao agregado um construtor marcado com `[JsonConstructor]`), em vez de usar o agregado de domínio como valor de cache.

## Objetivos (não medidos)

| Objetivo | Situação |
|---|---|
| 50 requisições por segundo na consulta | objetivo, não medido; existe só o limitador configurado com esse número |
| Latência baixa com cache | objetivo, não medido; não há teste de carga nem métrica de latência |
| Taxa de acerto por camada | não coletada |

## Consequências

### Positivas

- Leituras repetidas do mesmo dia, na mesma instância, não chegam ao banco por até 60 s.
- Falha do Redis não derruba a consulta: o `RedisCacheService` captura erro em leitura, gravação e remoção, e a consulta segue para o banco.

### Negativas

- **Leitura defasada até cerca de 60 s** depois de um lançamento, por causa da memória e do response cache, que não são invalidados. O `testar-fluxo-caixa.sh` usa um comerciante novo por cenário para não depender disso.
- Com mais de uma instância da API, cada uma tem a própria memória e o próprio response cache; duas consultas seguidas podem devolver valores diferentes.
- Três camadas para uma consulta que é uma busca por índice; parte da complexidade não se paga sem medição que a justifique.

## O que pode dar errado

- **Cache que só custa:** enquanto a desserialização falhar, toda leitura que passa da memória faz uma ida ao Redis que não serve para nada, grava de novo e registra um aviso com exceção no log.
- **Defasagem maior que a esperada:** se o Consolidado não conseguir apagar a chave (Redis fora), ele só registra um aviso e segue; com a leitura do Redis corrigida, a chave antiga valeria até 5 min.
- **Erro do cliente por dado velho:** quem lança e consulta logo em seguida não vê o lançamento. Não há cabeçalho nem campo na resposta que indique a idade do dado além de `ultimaAtualizacao`.
- **Memória sem limite:** o `IMemoryCache` não tem `SizeLimit`; muitas combinações de comerciante e data ocupam memória até expirar (1 min).
- **Limitador global:** a janela de 50 por segundo vale para todos os clientes juntos, sem partição por comerciante ou IP; um cliente pode consumir a cota dos outros.

## O que não foi feito

- Teste de carga (por exemplo, com NBomber ou k6) para medir vazão e latência.
- Métricas de acerto por camada.
- Invalidação da memória e do response cache.

## Referências

- [Caching guidance, Microsoft](https://learn.microsoft.com/azure/architecture/best-practices/caching)
- [Cache-Aside pattern, Microsoft](https://learn.microsoft.com/azure/architecture/patterns/cache-aside)

---

**Data:** 2026-01-15 (revisado em 2026-10-20)
