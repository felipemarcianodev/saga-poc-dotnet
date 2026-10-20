# ADR 003: PostgreSQL para escrita e leitura do fluxo de caixa

## Status

**Aceito** (implementado)

## Contexto

O fluxo de caixa usa CQRS ([ADR 001](./001-cqrs.md)) com dois modelos:

1. **Escrita (lançamentos):** um registro por lançamento, gravado numa transação; consultas por id e por comerciante e período.
2. **Leitura (consolidado):** uma linha por dia e comerciante com totais e quantidades, atualizada a cada evento e lida pela API de consulta.

A SAGA de pedidos já usava PostgreSQL para o estado do Rebus (`pedido_saga_data`), então a stack já tinha a tecnologia.

### Alternativas consideradas

| Alternativa | A favor | Contra |
|---|---|---|
| PostgreSQL nos dois modelos | transação ACID na escrita; mesmo provedor (Npgsql) e mesmas migrations do EF Core nos dois lados; já presente na stack | escala de escrita vertical; esquema exige migration |
| MongoDB no modelo de leitura | documento por dia combina com o consolidado; escala horizontal | mais uma tecnologia na stack; o consolidado é pequeno e tabular, sem ganho claro |
| SQL Server | bom suporte no EF Core | licença; imagem mais pesada para uma POC |
| TimescaleDB | feito para séries temporais | excessivo para uma linha por dia e comerciante |

## Decisão

**PostgreSQL 16** (`postgres:16-alpine`) para os dois modelos, em **instâncias separadas**:

| Container | Banco | Porta no host | Dono da escrita (e da migration) |
|---|---|---|---|
| `fluxocaixa-postgres-lancamentos` | `fluxocaixa_lancamentos` | 5433 | `lancamentos-worker` |
| `fluxocaixa-postgres-consolidado` | `fluxocaixa_consolidado` | 5434 | `fluxocaixa-consolidado` |

Acesso pelo EF Core com Npgsql e `EnableRetryOnFailure`. Cada worker aplica as próprias migrations ao subir; as APIs não migram, para não disputarem a mesma migration.

Instâncias separadas porque:
1. uma falha ou carga num banco não afeta o outro;
2. o modelo de leitura pode ser reconstruído a partir dos lançamentos, então pode ter política de backup diferente (hoje não há reconstrução implementada; ver abaixo).

### Esquema da escrita

Migration `CriarTabelaLancamentos`, tabela `lancamentos`:

| Coluna | Tipo |
|---|---|
| `id` | `uuid`, chave primária (gerado na aplicação) |
| `tipo` | `varchar(20)`, enum gravado como texto (`Debito`, `Credito`) |
| `valor` | `numeric(18,2)` |
| `data_lancamento` | `date` |
| `descricao` | `varchar(500)` |
| `comerciante` | `varchar(100)` |
| `categoria` | `varchar(100)`, opcional |
| `status` | `varchar(20)`, enum gravado como texto |
| `criado_em`, `atualizado_em` | `timestamp with time zone` |

Índices em (`comerciante`, `data_lancamento`), `data_lancamento` e `status`. As regras de valor e de tamanho ficam no domínio (`Lancamento.Criar`) e na API, não em `CHECK` no banco.

### Esquema da leitura

Migration `CriarTabelaConsolidado`, tabela `ConsolidadosDiarios`: `Id` (`uuid`), `Data`, `Comerciante` (`varchar(200)`), `TotalCreditos` e `TotalDebitos` (`numeric(18,2)`), `QuantidadeCreditos`, `QuantidadeDebitos` e `UltimaAtualizacao`. Índice único `IX_Consolidado_Data_Comerciante` em (`Data`, `Comerciante`).

`SaldoDiario` e `QuantidadeTotalLancamentos` são calculados no agregado e não são gravados.

## Consequências

### Positivas

- Uma tecnologia de banco para a POC inteira (SAGA e fluxo de caixa), com o mesmo provedor e o mesmo modelo de migration.
- Transação ACID na gravação do lançamento.
- O índice único em (`Data`, `Comerciante`) impede duas linhas para o mesmo dia: se dois eventos criarem o consolidado ao mesmo tempo, o segundo INSERT falha e o Rebus repete a mensagem, que então encontra a linha.

### Negativas

- Escala de escrita vertical; réplicas de leitura e particionamento não foram configurados.
- O esquema muda só por migration.
- A leitura depende de cache para aguentar volume ([ADR 002](./002-cache.md)), e nenhuma vazão foi medida.

## O que pode dar errado

- **Atualização perdida:** o consolidado é lido, alterado em memória e gravado, sem coluna de versão nem `UPDATE ... SET total = total + valor`. Dois eventos do mesmo dia em paralelo perderiam um valor. Hoje isso é evitado processando uma mensagem por vez (`MaxParallelism = 1`).
- **Sem reconstrução do consolidado:** a ideia de que a leitura "pode ser reconstruída" não está implementada. O agregado tem `Recalcular`, mas nenhum job ou endpoint o usa, e os dois bancos estão em instâncias diferentes, então não há consulta que junte os dois.
- **Sem auditoria:** não há tabela de histórico nem trigger; o lançamento só guarda `criado_em` e `atualizado_em`.
- **Fuso da data:** `data_lancamento` é `date` na escrita, e `Data` é `timestamp with time zone` na leitura; o Consolidado grava o dia como meia-noite UTC. Um cliente em outro fuso precisa enviar a data já no dia certo.
- **Credenciais fixas:** usuário e senha (`saga`/`saga123`) estão no compose, aceitável só para uso local.

## Referências

- [Documentação do PostgreSQL 16](https://www.postgresql.org/docs/16/)
- [Npgsql Entity Framework Core Provider](https://www.npgsql.org/efcore/)

---

**Data:** 2026-01-15 (revisado em 2026-10-20)
