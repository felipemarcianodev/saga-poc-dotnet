# POC SAGA Pattern com MassTransit e RabbitMQ

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp)
![MassTransit](https://img.shields.io/badge/MassTransit-9.0-orange)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3.13-FF6600?logo=rabbitmq)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker)

**Proof of Concept** demonstrando a implementação do **padrão SAGA Orquestrado** utilizando **MassTransit** e **RabbitMQ** para comunicação entre microsserviços, com aplicação do **Result Pattern** para tratamento estruturado de erros.

---

## Sobre o Projeto

### Domínio
Sistema de **Delivery de Comida** simulando um fluxo completo de processamento de pedidos.

### Objetivo
Demonstrar como implementar:
- **SAGA Orquestrado** com MassTransit State Machine
- **Compensações automáticas** em caso de falha
- **Result Pattern** para tratamento de erros sem exceções
- **Mensageria assíncrona** com RabbitMQ
- **Idempotência** nas operações de compensação
- **Stack 100% Open Source** - Sem dependências de cloud

---

## Arquitetura

### Fluxo da SAGA

![Diagrama Visual do Fluxo](./images/diagrama-visual-fluxo.png)

### Compensações em Cascata

Quando ocorre uma falha em qualquer etapa, as compensações são executadas **em ordem reversa**:

![Diagrama de compensação em cascata](./images/diagrama-compensacao-saga-cascata.png)
---

## Estrutura do Projeto

![Diagrama da Estrutura do Projeto](./images/diagrama-estrutura-projeto.png)
---

## Como Executar

### Pré-requisitos

- **.NET 9 SDK** ou superior ([Download](https://dotnet.microsoft.com/download/dotnet/9.0))
- **Docker Desktop** ([Download](https://www.docker.com/products/docker-desktop))
- **Git**

### 1. Clonar o Repositório

```bash
git clone https://github.com/seu-usuario/saga-poc-dotnet.git
cd saga-poc-dotnet
```

### 2. Subir o RabbitMQ com Docker

Na raiz do projeto, execute:

```bash
docker-compose up -d
```

Isso irá:
- Baixar a imagem do **RabbitMQ 3.13** com Management UI
- Iniciar o RabbitMQ na porta **5672** (AMQP)
- Disponibilizar a interface web na porta **15672**

### 3. Acessar o RabbitMQ Management UI

Abra seu navegador e acesse:

```
http://localhost:15672
```

**Credenciais:**
- **Usuário**: `saga`
- **Senha**: `saga123`

Você verá a interface de gerenciamento onde poderá monitorar:
- **Queues** (filas de mensagens)
- **Exchanges** (roteadores de mensagens)
- **Connections** (conexões ativas)
- **Channels** (canais de comunicação)

### 4. Executar os Serviços

#### Opção 1: Manualmente (6 terminais)

```bash
# Terminal 1: API
cd src/SagaPoc.Api
dotnet run

# Terminal 2: Orquestrador
cd src/SagaPoc.Orquestrador
dotnet run

# Terminal 3: Serviço Restaurante
cd src/SagaPoc.ServicoRestaurante
dotnet run

# Terminal 4: Serviço Pagamento
cd src/SagaPoc.ServicoPagamento
dotnet run

# Terminal 5: Serviço Entregador
cd src/SagaPoc.ServicoEntregador
dotnet run

# Terminal 6: Serviço Notificação
cd src/SagaPoc.ServicoNotificacao
dotnet run
```

### 5. Acessar a API

- **Swagger UI**: http://localhost:5000 ou http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health

### 6. Monitorar as Filas no RabbitMQ

Acesse o **RabbitMQ Management UI** em http://localhost:15672 e clique na aba **Queues**.

Você verá as seguintes filas sendo criadas automaticamente pelo MassTransit:

- **`fila-restaurante`** - Mensagens para validação de pedidos no restaurante
- **`fila-pagamento`** - Mensagens para processamento de pagamentos
- **`fila-entregador`** - Mensagens para alocação de entregadores
- **`fila-notificacao`** - Mensagens para notificações aos clientes
- **`fila-dead-letter`** - Mensagens que falharam após todas as tentativas de retry

Ao fazer requisições à API, você poderá ver em tempo real:
- **Ready**: Mensagens aguardando processamento
- **Unacked**: Mensagens sendo processadas no momento
- **Total**: Total de mensagens que passaram pela fila

### 7. Parar os Serviços

#### Parar os serviços .NET
Pressione `Ctrl+C` em cada terminal.

#### Parar o RabbitMQ
```bash
docker-compose down
```

#### Limpar volumes do Docker (opcional)
```bash
docker-compose down -v
```

---

## Testando os Casos de Uso

### 12 Cenários Implementados

![12 Cenários implementados](./images/12-cenarios-implementados.png)
---

### Via Scripts Automatizados

#### Windows (PowerShell):
```powershell
cd docs/scripts
.\testar-casos-de-uso.ps1        # Testa todos os 12 casos
.\testar-casos-de-uso.ps1 5      # Testa apenas o caso 5
```

#### Linux/Mac (Bash):
```bash
cd docs/scripts
./testar-casos-de-uso.sh         # Testa todos os 12 casos
./testar-casos-de-uso.sh 5       # Testa apenas o caso 5
```

### Via curl (Exemplo: Caso 1 - Pedido Normal)

```bash
curl -X POST http://localhost:5000/api/pedidos \
  -H "Content-Type: application/json" \
  -d '{
    "clienteId": "CLI001",
    "restauranteId": "REST001",
    "itens": [
      {
        "produtoId": "PROD001",
        "nome": "Pizza Margherita",
        "quantidade": 1,
        "precoUnitario": 45.90
      }
    ],
    "enderecoEntrega": "Rua das Flores, 123",
    "formaPagamento": "CREDITO"
  }'
```

**Resposta esperada**:
```json
{
  "pedidoId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "mensagem": "Pedido recebido e está sendo processado.",
  "status": "Pendente"
}
```

### Observando os Logs

Cada serviço gera logs estruturados com Serilog. Exemplo de fluxo completo:

```
[INFO] Validando pedido no restaurante REST001 com 1 itens
[INFO] Pedido validado. ValorTotal: R$ 45,90, TempoPreparo: 10min
[INFO] Processando pagamento. ClienteId: CLI001, Valor: R$ 45,90
[INFO] Pagamento aprovado. TransacaoId: TXN_abc123
[INFO] Alocando entregador. RestauranteId: REST001
[INFO] Entregador ENT001 alocado. TempoEstimado: 25min
[INFO] Notificação enviada com sucesso. Tipo: PedidoConfirmado
[INFO] SAGA finalizada com sucesso. Estado: PedidoConfirmado
```

---

## Documentação Completa

### Documentos Principais

- **[casos-uso.md](docs/casos-uso.md)** - Detalhamento completo dos 12 cenários com payloads
- **[plano-execucao.md](docs/plano-execucao.md)** - Plano de execução em 15 fases
- **[arquitetura.md](docs/arquitetura.md)** - Detalhes da arquitetura e decisões técnicas
- **[guia-masstransit.md](docs/guia-masstransit.md)** - Guia de uso do MassTransit

### Documentação Operacional (Fase 14)

- **[diagramas-compensacao.md](docs/diagramas-compensacao.md)** - Diagramas detalhados dos fluxos de compensação e estados da SAGA
- **[runbook-troubleshooting.md](docs/runbook-troubleshooting.md)** - Guia de diagnóstico e resolução de problemas comuns
- **[boas-praticas.md](docs/boas-praticas.md)** - Guia de boas práticas para implementação e operação de SAGAs

### Scripts de Teste

- **[docs/scripts/readme-script.md](docs/scripts/readme-script.md)** - Como usar os scripts de teste

---

## Tecnologias Utilizadas

![Tech Stack](./images/tech-stack.png)  

---

## Conceitos Demonstrados

### 1. SAGA Orquestrado
- State Machine centralizada (MassTransit)
- Controle de fluxo e transições de estado
- Persistência do estado (InMemory para POC)
- Veja **[Diagramas de Compensação](docs/diagramas-compensacao.md)** para detalhes visuais

### 2. Compensações Automáticas
- Rollback em ordem reversa
- Idempotência (executar 2x não causa problema)
- Tratamento de erros estruturado
- Consulte **[Boas Práticas](docs/boas-praticas.md)** para implementação correta

### 3. Result Pattern
- Encapsulamento de sucesso/falha
- Sem exceções para controle de fluxo
- Propagação de erros estruturados

### 4. Mensageria Assíncrona
- Request/Response via MassTransit
- Publish/Subscribe para eventos
- Dead Letter Queue automática

### Aprenda Mais

Para entender como implementar corretamente cada conceito, consulte:
- **[Boas Práticas](docs/boas-praticas.md)** - Os 10 mandamentos da SAGA, com exemplos de código
- **[Diagramas de Compensação](docs/diagramas-compensacao.md)** - Visualização completa dos fluxos

---

## Observabilidade

### Logs Estruturados (Serilog)

Cada operação gera logs com:
- **CorrelationId** (rastreamento end-to-end)
- **Transições de estado** da SAGA
- **Compensações executadas**
- **Timestamps** e métricas

### Rastreamento de SAGA

```bash
# Filtrar logs por PedidoId
grep "a1b2c3d4-e5f6-7890-abcd-ef1234567890" logs/*.log
```

### Ferramentas Recomendadas

- **Seq** - Visualizador de logs estruturados (Serilog)
- **Jaeger** - Distributed tracing

### Troubleshooting

Para diagnosticar e resolver problemas comuns, consulte o **[Runbook de Troubleshooting](docs/runbook-troubleshooting.md)** que cobre:
- SAGA travada
- Mensagens em Dead Letter Queue
- Compensação falhou
- Alta latência nas SAGAs
- Circuit breaker aberto
- Perda de mensagens
- Duplicação de pedidos

E muitos outros cenários com diagnóstico passo a passo e ações corretivas.

---

## Próximos Passos (Para Produção)

Esta POC é **educacional**. Para produção, considere:

### 1. Persistência da SAGA
- Trocar `InMemoryRepository` por **SQL Server** ou **Redis**
- Garantir recuperação em caso de reinicialização

### 2. Outbox Pattern
- Garantir atomicidade entre banco de dados e mensagens
- Evitar perda de mensagens

### 3. Retry Policy e Circuit Breaker
- Configurar retry exponencial
- Proteger serviços downstream

### 4. Idempotência
- Deduplicação de mensagens por MessageId
- Armazenamento em Redis/SQL

### 5. Observabilidade
- OpenTelemetry + Application Insights
- Métricas e dashboards

### 6. Testes
- Testes de integração automatizados
- Testes de carga (NBomber)
- Chaos Engineering

Veja mais detalhes em [plano-execucao.md - Seção 9](docs/plano-execucao.md#9-pr%C3%B3ximos-passos-opcionais---produ%C3%A7%C3%A3o).

---

## Licença

Este projeto é licenciado sob a [MIT License](LICENSE).

---

## Contribuindo

Contribuições são bem-vindas! Sinta-se à vontade para:
- Reportar bugs
- Sugerir melhorias
- Adicionar novos casos de uso
- Melhorar a documentação

---

## Contato

Criado como material educacional sobre padrões de microsserviços.

---

## Agradecimentos

- [MassTransit](https://masstransit.io/) - Excelente framework de mensageria
- [RabbitMQ](https://www.rabbitmq.com/) - Message broker open source confiável e battle-tested
- [Chris Richardson](https://microservices.io/patterns/data/saga.html) - Padrão SAGA
- [Docker](https://www.docker.com/) - Containerização e simplificação de deploy

**Última atualização**: 2026-01-07 - Fase 14 concluída (Documentação Operacional)