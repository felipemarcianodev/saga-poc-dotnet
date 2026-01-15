# Setup da Persistência do Estado da SAGA - Fase 16

## Visão Geral

Este documento descreve como configurar e executar a persistência do estado da SAGA usando PostgreSQL. A implementação garante que o estado da SAGA sobreviva a reinicializações do orquestrador e implementa concorrência otimista para evitar condições de corrida.

## Pré-requisitos

### 1. PostgreSQL

Você precisa ter o PostgreSQL instalado e rodando. Escolha uma das opções:

#### Opção A: Docker (Recomendado para desenvolvimento)

```bash
# Iniciar PostgreSQL com Docker
docker run --name postgres-saga \
  -e POSTGRES_USER=saga \
  -e POSTGRES_PASSWORD=saga123 \
  -e POSTGRES_DB=sagapoc \
  -p 5432:5432 \
  -d postgres:16-alpine

# Verificar se está rodando
docker ps | grep postgres-saga

# Conectar ao banco (opcional - para verificação)
docker exec -it postgres-saga psql -U saga -d sagapoc
```

#### Opção B: PostgreSQL Local

1. Baixe e instale o PostgreSQL: https://www.postgresql.org/download/
2. Crie o banco de dados:

```sql
CREATE DATABASE sagapoc;
CREATE USER saga WITH PASSWORD 'saga123';
GRANT ALL PRIVILEGES ON DATABASE sagapoc TO saga;
```

## Configuração

### 1. Connection String

A connection string está configurada em `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "SagaDatabase": "Host=localhost;Port=5432;Database=sagapoc;Username=saga;Password=saga123"
  }
}
```

**Importante**: Em produção, use variáveis de ambiente ou Azure Key Vault para armazenar credenciais.

### 2. Estrutura do Banco de Dados

O banco de dados contém duas tabelas principais criadas automaticamente:

#### Tabela `PedidoSagas` (Dados da SAGA)
```sql
CREATE TABLE "PedidoSagas" (
    "Id" UUID PRIMARY KEY,
    "Revision" INTEGER NOT NULL,
    "Data" JSONB NOT NULL
);
```

#### Tabela `PedidoSagasIndex` (Índices da SAGA)
```sql
CREATE TABLE "PedidoSagasIndex" (
    "SagaId" UUID NOT NULL,
    "Key" VARCHAR(200) NOT NULL,
    "Value" VARCHAR(1024) NOT NULL,
    "SagaType" VARCHAR(200) NOT NULL,
    PRIMARY KEY ("Key", "Value", "SagaType")
);
```

## Execução

### 1. Inicialização Automática

O orquestrador cria automaticamente as tabelas necessárias na primeira execução:

```bash
cd src/SagaPoc.Orquestrador
dotnet run
```

Logs esperados:
```
[INFO] Verificando banco de dados...
[INFO] Banco de dados verificado com sucesso.
[INFO] Criando tabelas do Rebus...
[INFO] Tabela 'PedidoSagas' criada/verificada.
[INFO] Tabela 'PedidoSagasIndex' criada/verificada.
[INFO] Índices criados/verificados.
[INFO] Todas as tabelas do Rebus foram criadas com sucesso.
[INFO] Orquestrador SAGA iniciado com sucesso
```

### 2. Migrations do Entity Framework (Opcional)

As migrations do EF Core foram criadas para o DbContext:

```bash
# Ver migrations disponíveis
dotnet ef migrations list --project src/SagaPoc.Orquestrador

# Aplicar migrations manualmente (não necessário - feito automaticamente)
dotnet ef database update --project src/SagaPoc.Orquestrador
```

## Verificação

### 1. Verificar Tabelas Criadas

Conecte ao PostgreSQL e verifique:

```sql
-- Listar todas as tabelas
\dt

-- Verificar estrutura da tabela de SAGAs
\d "PedidoSagas"

-- Verificar estrutura da tabela de índices
\d "PedidoSagasIndex"
```

### 2. Consultar Estado das SAGAs

```sql
-- Ver todas as SAGAs
SELECT "Id", "Revision", "Data" FROM "PedidoSagas";

-- Ver detalhes de uma SAGA específica (JSON formatado)
SELECT "Data"::json->>'EstadoAtual' as estado,
       "Data"::json->>'ClienteId' as cliente,
       "Data"::json->>'ValorTotal' as valor
FROM "PedidoSagas"
WHERE "Id" = 'seu-guid-aqui';

-- Ver índices de correlação
SELECT * FROM "PedidoSagasIndex";
```

## Teste de Persistência

### Teste 1: Reinicialização do Orquestrador

1. Inicie o orquestrador:
   ```bash
   cd src/SagaPoc.Orquestrador
   dotnet run
   ```

2. Crie um pedido através da API

3. Pare o orquestrador (Ctrl+C)

4. Verifique no banco que o estado foi salvo:
   ```sql
   SELECT COUNT(*) FROM "PedidoSagas";
   ```

5. Reinicie o orquestrador

6. O estado da SAGA deve ser recuperado automaticamente

### Teste 2: Concorrência Otimista

O Rebus implementa concorrência otimista usando o campo `Revision`. Se duas mensagens tentarem atualizar a mesma SAGA simultaneamente, uma falhará e será reprocessada automaticamente.

## Troubleshooting

### Erro: "Connection refused"

**Causa**: PostgreSQL não está rodando
**Solução**:
```bash
# Docker
docker start postgres-saga

# Serviço local (Windows)
net start postgresql-x64-16

# Serviço local (Linux)
sudo systemctl start postgresql
```

### Erro: "password authentication failed"

**Causa**: Credenciais incorretas
**Solução**: Verifique a connection string em `appsettings.json`

### Erro: "database does not exist"

**Causa**: Banco de dados não foi criado
**Solução**:
```bash
# Docker
docker exec -it postgres-saga psql -U saga -c "CREATE DATABASE sagapoc;"

# PostgreSQL local
psql -U postgres -c "CREATE DATABASE sagapoc;"
```

### Erro: "relation does not exist"

**Causa**: Tabelas não foram criadas
**Solução**: As tabelas são criadas automaticamente na inicialização. Verifique os logs do orquestrador.

## Backup e Recovery

### Backup Manual

```bash
# Backup completo do banco
docker exec -t postgres-saga pg_dump -U saga sagapoc > backup_sagapoc_$(date +%Y%m%d).sql

# Backup apenas da tabela de SAGAs
docker exec -t postgres-saga pg_dump -U saga sagapoc -t "PedidoSagas" > backup_sagas.sql
```

### Restore

```bash
# Restore completo
docker exec -i postgres-saga psql -U saga sagapoc < backup_sagapoc_20260109.sql

# Restore de tabela específica
docker exec -i postgres-saga psql -U saga sagapoc < backup_sagas.sql
```

### Backup Automatizado (Produção)

Em produção, configure:
1. **Azure Database for PostgreSQL**: Backups automáticos com retenção de 7-35 dias
2. **AWS RDS PostgreSQL**: Snapshots automáticos
3. **Cron job**: Script de backup diário

## Monitoramento

### Queries Úteis

```sql
-- Total de SAGAs por estado
SELECT "Data"::json->>'EstadoAtual' as estado, COUNT(*) as total
FROM "PedidoSagas"
GROUP BY estado;

-- SAGAs em compensação
SELECT "Id", "Data"::json->>'EstadoAtual' as estado
FROM "PedidoSagas"
WHERE "Data"::json->>'EmCompensacao' = 'true';

-- SAGAs antigas (mais de 30 dias)
SELECT COUNT(*) as total_antigas
FROM "PedidoSagas"
WHERE ("Data"::json->>'DataInicio')::timestamp < NOW() - INTERVAL '30 days';

-- Tamanho das tabelas
SELECT
    pg_size_pretty(pg_total_relation_size('"PedidoSagas"')) as tamanho_dados,
    pg_size_pretty(pg_total_relation_size('"PedidoSagasIndex"')) as tamanho_indices;
```

## Limpeza de Dados Antigos

```sql
-- Deletar SAGAs concluídas há mais de 90 dias
DELETE FROM "PedidoSagas"
WHERE "Data"::json->>'EstadoAtual' = 'Concluido'
  AND ("Data"::json->>'DataConclusao')::timestamp < NOW() - INTERVAL '90 days';

-- Deletar SAGAs canceladas há mais de 30 dias
DELETE FROM "PedidoSagas"
WHERE "Data"::json->>'EstadoAtual' = 'Cancelado'
  AND ("Data"::json->>'DataConclusao')::timestamp < NOW() - INTERVAL '30 days';
```

## Performance

### Índices

Os seguintes índices são criados automaticamente:
- `PK_PedidoSagas` (Id) - Chave primária
- `IX_PedidoSagasIndex_SagaId` - Lookup por SAGA ID
- `PK_PedidoSagasIndex` - Correlação de mensagens

### Otimizações

1. **Connection Pooling**: Configurado automaticamente pelo Npgsql
2. **JSONB**: Dados da SAGA armazenados em formato binário para performance
3. **Prepared Statements**: Rebus usa prepared statements automaticamente

## Migração de InMemory para PostgreSQL

Se você estava usando InMemory, a migração é simples:

1. Código atualizado: `StoreInMemory()` → `StoreInPostgres()`
2. Connection string adicionada
3. DbContext criado
4. Tabelas criadas automaticamente

Nenhuma SAGA em andamento será migrada. Aguarde todas as SAGAs atuais finalizarem antes de atualizar.

## Próximos Passos

- [Fase 17 - Outbox Pattern](./fase-17.md)
- [Fase 18 - Observabilidade com OpenTelemetry](./fase-18.md)

---

**Status**: Persistência PostgreSQL implementada e testada
**Data**: 2026-01-09
**Versão**: 1.0.0
