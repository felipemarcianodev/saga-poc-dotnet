# SAGA POC - Docker Compose

Este diretório contém a configuração Docker Compose completa para executar toda a stack da SAGA POC, incluindo serviços .NET, RabbitMQ e observabilidade (Jaeger, Prometheus, Grafana).

## Pré-requisitos

- Docker Desktop instalado e em execução
- Pelo menos 4GB de RAM disponível para os containers

## Serviços Incluídos

### Infraestrutura
- **RabbitMQ** (porta 15672): Message broker com interface de gerenciamento
- **Jaeger** (porta 16686): Distributed tracing UI
- **Prometheus** (porta 9090): Coleta de métricas
- **Grafana** (porta 3000): Dashboards e visualização
- **Node Exporter** (porta 9100): Métricas do sistema

### Serviços .NET
- **saga-api** (porta 5000): API REST principal
- **saga-orquestrador**: SAGA State Machine (Orquestrador)
- **saga-servico-restaurante**: Worker de validação de restaurante
- **saga-servico-pagamento**: Worker de processamento de pagamento
- **saga-servico-entregador**: Worker de alocação de entregador
- **saga-servico-notificacao**: Worker de notificações

## Como Executar

### 1. Iniciar toda a stack

```bash
cd C:\Projetos\saga-poc-dotnet\docker
docker-compose up -d
```

### 2. Acompanhar os logs

```bash
# Todos os serviços
docker-compose logs -f

# Apenas um serviço específico
docker-compose logs -f saga-api
docker-compose logs -f saga-orquestrador
```

### 3. Verificar status dos containers

```bash
docker-compose ps
```

### 4. Parar a stack

```bash
docker-compose down
```

### 5. Parar e remover volumes (limpar dados)

```bash
docker-compose down -v
```

## URLs de Acesso

Após executar `docker-compose up`, você pode acessar:

- **API Swagger**: http://localhost:5000
- **RabbitMQ Management**: http://localhost:15672 (usuário: `saga`, senha: `saga123`)
- **Jaeger UI**: http://localhost:16686 (traces distribuídos)
- **Prometheus**: http://localhost:9090 (métricas)
- **Grafana**: http://localhost:3000 (usuário: `admin`, senha: `admin123`)

## Rebuild dos Serviços .NET

Se você fez alterações no código dos serviços .NET, precisa rebuildar as imagens:

```bash
# Rebuild de todos os serviços
docker-compose build

# Rebuild de um serviço específico
docker-compose build saga-api

# Rebuild e restart
docker-compose up -d --build
```

## Troubleshooting

### Containers não iniciam
```bash
# Verificar logs
docker-compose logs

# Verificar se há conflito de portas
netstat -ano | findstr :5000
netstat -ano | findstr :5672
```

### RabbitMQ não está pronto
Os serviços .NET aguardam o RabbitMQ ficar healthy. Se demorar muito:
```bash
# Verificar logs do RabbitMQ
docker-compose logs rabbitmq

# Reiniciar apenas o RabbitMQ
docker-compose restart rabbitmq
```

### Limpar tudo e recomeçar
```bash
# Parar e remover tudo
docker-compose down -v

# Remover imagens antigas
docker-compose down --rmi all -v

# Rebuild completo
docker-compose build --no-cache
docker-compose up -d
```

## Estrutura de Arquivos

```
docker/
├── docker-compose.yml              # Configuração principal
├── infra/
│   ├── prometheus/
│   │   └── prometheus.yml          # Configuração Prometheus
│   └── grafana/
│       ├── datasources/
│       │   └── datasources.yml     # Datasources (Prometheus + Jaeger)
│       └── dashboards.yml          # Configuração de dashboards
└── README.md                        # Este arquivo
```

## Observabilidade

### Jaeger (Distributed Tracing)
- Acesse http://localhost:16686
- Selecione o serviço (ex: `SagaPoc.Api`)
- Visualize os traces das requisições end-to-end

### Prometheus (Métricas)
- Acesse http://localhost:9090
- Exemplos de queries:
  - Taxa de requisições: `rate(http_server_requests_total[5m])`
  - Duração P95: `histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))`

### Grafana (Dashboards)
- Acesse http://localhost:3000 (admin/admin123)
- Datasources já configurados: Prometheus e Jaeger
- Crie seus próprios dashboards ou importe existentes

## Rede Docker

Todos os serviços estão na mesma rede (`saga-network`), permitindo comunicação entre eles usando os nomes dos containers:
- `rabbitmq:5672`
- `jaeger:6831`
- `prometheus:9090`
- `saga-api:8080`

## Variáveis de Ambiente

As configurações são definidas no `docker-compose.yml`:
- `RabbitMQ__Host=rabbitmq`
- `Jaeger__AgentHost=jaeger`
- `ASPNETCORE_ENVIRONMENT=Development`

Para sobrescrever, crie um arquivo `.env` no mesmo diretório do `docker-compose.yml`.
