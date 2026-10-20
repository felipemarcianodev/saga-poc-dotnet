# Scripts de Teste - Sistema Completo

Scripts automatizados e interativos para testar **TODO** o sistema:
- **SAGA Pattern** (Delivery de Comida - 12 cenários gerais)
- **Contextos SAGA** (Restaurante, Pagamento, Entregador, Notificação)
- **Fluxo de Caixa** (CQRS + Event-Driven - 4 cenários)
- **Sistema Completo** (Teste de carga e monitoramento em tempo real)

---

## COMECE AQUI: Menu Interativo

### `menu-interativo.ps1` - Recomendado para Iniciantes

**Menu visual com TODAS as opções de teste em um só lugar!**

```powershell
cd C:\Projetos\saga-poc-dotnet\docs\scripts
.\menu-interativo.ps1
```

**Recursos:**
- Interface amigável com cores
- Testa SAGA (12 cenários)
- Testa Fluxo de Caixa (4 cenários)
- Teste de carga automático
- Monitor em tempo real
- Verificação de saúde dos serviços
- Estatísticas do RabbitMQ
- Consulta de consolidados

---

## Scripts do SAGA Pattern

### 1. `testar-casos-de-uso.sh` / `testar-casos-de-uso.ps1`

Testa os 12 casos de uso do SAGA Pattern (delivery de comida).

A versão Bash confere o **desfecho real** de cada pedido. A API responde 202 assim que aceita o pedido, o que não prova que a SAGA terminou; por isso o script lê os logs do orquestrador (`docker logs saga-orquestrador`) e compara o desfecho de cada pedido com o esperado. Sai com código 1 se algum caso divergir, então serve para validação automatizada.

**Uso (Bash, inclusive Git Bash no Windows):**

```bash
# Testar todos os casos
./testar-casos-de-uso.sh

# Testar um caso específico (1-12)
./testar-casos-de-uso.sh 5

# Outra URL de API ou outro tempo máximo de espera
BASE_URL=http://localhost:8080 TEMPO_MAXIMO=90 ./testar-casos-de-uso.sh
```

**Uso (PowerShell):**

```powershell
.\testar-casos-de-uso.ps1
.\testar-casos-de-uso.ps1 5
.\testar-casos-de-uso.ps1 -BaseUrl "http://localhost:8080"
```

**Casos e desfecho esperado:**

| Caso | Cenário | Desfecho esperado |
|---|---|---|
| 1 | Pedido normal (happy path) | Concluído |
| 2 | Restaurante fechado | Cancelado |
| 3 | Item indisponível | Cancelado |
| 4 | Pagamento recusado | Compensado |
| 5 | Sem entregador (endereço fora da área) | Compensado |
| 6 | Timeout no pagamento | Compensado |
| 7 | Pedido premium (VIP) | Concluído |
| 8 | Múltiplos itens | Concluído |
| 9 | Endereço longe | Concluído |
| 10 | Falha na notificação | Concluído |
| 11 | Pedido agendado | Concluído |
| 12 | Compensação total | Compensado |

- **Cancelado:** a SAGA para antes de qualquer passo que precise ser desfeito.
- **Compensado:** um passo falhou depois de outros terem sido executados, e a SAGA desfez os anteriores (estorno, liberação do entregador, cancelamento no restaurante).

> **Horário de pico simulado:** entre 12h e 14h59 (UTC), o serviço de entregador recusa 30% das alocações. Nessa janela, um caso que deveria concluir pode terminar compensado; o script marca isso como **AVISO**, e não como falha. Para desligar a simulação, suba a stack com `SIMULAR_HORARIO_PICO=false` (é o que o CI faz).

---

## Scripts do Fluxo de Caixa

### 2. `testar-fluxo-caixa.sh`

Testa o fluxo de caixa (CQRS) de ponta a ponta e confere o resultado real: lançamentos pela API de escrita (porta 5200), gravação e publicação pelo worker, aplicação dos eventos pelo Consolidado e leitura pela API de consulta (porta 5100).

O 202 da API de lançamentos não prova nada. O script espera as filas `fluxocaixa-lancamentos` e `fluxocaixa-consolidado` esvaziarem e só então confere os lançamentos gravados e os valores do consolidado. Cada cenário usa um comerciante novo, então o cache da API de consulta não interfere entre rodadas.

**Uso (Git Bash ou Linux, com a stack no ar):**

```bash
bash docs/scripts/testar-fluxo-caixa.sh

# Outras URLs ou tempo de espera
LANCAMENTOS_URL=http://localhost:5200 CONSULTA_URL=http://localhost:5100 TEMPO_MAXIMO=90 bash docs/scripts/testar-fluxo-caixa.sh
```

| Cenário | O que envia | O que confere |
|---|---|---|
| 1 | Crédito de 150,50 e débito de 40,25 no mesmo dia | 2 lançamentos confirmados; saldo 110,25 |
| 2 | 5 créditos (100 a 500) e 3 débitos (50, 75, 125) | 8 lançamentos; créditos 1500, débitos 250, saldo 1250 |
| 3 | Crédito de 1000 ontem e débito de 250 hoje | Dois consolidados separados: saldo 1000 ontem e -250 hoje |
| 4 | Valor zero, descrição e comerciante curtos demais | HTTP 400 e nenhum lançamento gravado |

Ao final, confere que nenhuma mensagem nova caiu na fila `error`. Sai com código 1 se qualquer verificação falhar.

**`testar-fluxo-caixa.ps1` (legado):** usa uma única URL para as duas APIs e não confere o resultado; será revisto na fase 6 do plano de retomada. Use o `.sh`.

---

## Antes dos testes de ponta a ponta

### `aguardar-stack.sh`

Espera a stack ficar pronta depois de um `docker compose up`. Container no ar não basta: mensagem enviada antes de o orquestrador declarar a fila dele, ou antes de o Consolidado assinar os eventos, é descartada pelo RabbitMQ sem erro. O script só libera quando as três APIs respondem 200 em `/health`, as sete filas existem e as duas assinaturas do Consolidado estão no exchange `RebusTopics`.

```bash
bash docs/scripts/aguardar-stack.sh
TEMPO_MAXIMO=300 bash docs/scripts/aguardar-stack.sh   # padrão: 180 s
```

Sai com código 1 e lista o que ficou pendente se o tempo acabar. O CI (`.github/workflows/ci.yml`) roda este script e depois os dois testes acima.

---

## Scripts do Sistema Completo

### 3. `testar-sistema-completo.ps1` 🆕

Testa **SAGA + Fluxo de Caixa simultaneamente** com dashboard em tempo real.

**Uso:**

```powershell
# Teste de 60 segundos (padrão)
.\testar-sistema-completo.ps1

# Teste de 120 segundos
.\testar-sistema-completo.ps1 -DuracaoSegundos 120

# Alterar URLs
.\testar-sistema-completo.ps1 -BaseUrlSaga "http://localhost:5000" -BaseUrlFluxoCaixa "http://localhost:5100"
```

**O que faz:**
- Envia pedidos SAGA a cada 5 segundos
- Envia lançamentos FluxoCaixa a cada 5 segundos
- Mostra dashboard atualizado em tempo real
- Exibe estatísticas do RabbitMQ
- Gera relatório final com métricas

**Exemplo de Dashboard:**

```
╔══════════════════════════════════════════════════════════════════════╗
║               DASHBOARD DO SISTEMA COMPLETO                    ║
║  Tempo decorrido: 01:30                                              ║
╚══════════════════════════════════════════════════════════════════════╝

┌──────────────────────────────────────────────────────────────────────┐
│  SAGA PATTERN (Delivery de Comida)                                │
├──────────────────────────────────────────────────────────────────────┤
│  Pedidos com Sucesso:   15
│  Pedidos com Falha:      3 ❌
│  Taxa de Sucesso:       83%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  FLUXO DE CAIXA (CQRS + Event-Driven)                             │
├──────────────────────────────────────────────────────────────────────┤
│  Lançamentos Criados:   42 📊
│  Erros de Validação:     2 ⚠️
│  Taxa de Sucesso:       95%
└──────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────┐
│  🐰 RABBITMQ - Filas                                                 │
├──────────────────────────────────────────────────────────────────────┤
│  fila-orquestrador              Ready:     0 Unacked:     2 │
│  fluxocaixa-consolidado         Ready:     5 Unacked:     1 │
└──────────────────────────────────────────────────────────────────────┘
```

---

### 4. `monitor-tempo-real.ps1` 🆕

Monitor completo do sistema com atualização automática a cada 2 segundos.

**Uso:**

```powershell
# Monitorar com intervalo padrão (2s)
.\monitor-tempo-real.ps1

# Monitorar com intervalo de 5s
.\monitor-tempo-real.ps1 -IntervalSegundos 5

# Pressione Ctrl+C para sair
```

**O que monitora:**
- Status das APIs (SAGA e Fluxo de Caixa)
- Filas do RabbitMQ (mensagens ready, unacked)
- Throughput de mensagens (msg/s)
- Consolidados do dia (valores em tempo real)
- Gráficos de barras no terminal

**Exemplo de Saída:**

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                  🔄 MONITOR EM TEMPO REAL - SISTEMA COMPLETO                 ║
║  Atualizado às: 14:35:22                                                     ║
╚══════════════════════════════════════════════════════════════════════════════╝

┌─────────────────────────────────────────────────────────────────────────────┐
│  🌐 APIS                                                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  SAGA Pattern API        ONLINE   http://localhost:5000
│  Fluxo de Caixa API      ONLINE   http://localhost:5000
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  🐰 RABBITMQ                                                                │
├─────────────────────────────────────────────────────────────────────────────┤
│  Status: ONLINE
│  Conexões:   5    Canais:  12    Filas:  8
│
│  FILAS DO SISTEMA:
│
│    fila-orquestrador
│      Ready:    0 ███░░░░░░░░░░░░░
│      Unack:    2 ██████░░░░░░░░░░
│    fluxocaixa-consolidado
│      Ready:    5 ████████████░░░░
│      Unack:    1 ███░░░░░░░░░░░░░
│
│  THROUGHPUT:
│    Publicação: 12.50 msg/s
│    Consumo:    11.80 msg/s
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  FLUXO DE CAIXA - CONSOLIDADO DO DIA                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│  COM001 - 2026-01-15
│    Créditos:  R$    1.250,00  (8 lançtos)
│    Débitos:   R$      380,00  (3 lançtos)
│    ─────────────────────────────────
│    Saldo:     R$      870,00
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Scripts por Contexto (SAGA Pattern)

Scripts especializados para testar cada contexto do SAGA Pattern individualmente.

### 5. `testar-restaurante.ps1` 🆕

Testa o contexto de validação de pedidos no restaurante.

**Uso:**

```powershell
# Testar TODOS os cenários
.\testar-restaurante.ps1

# Testar cenário específico (1-4)
.\testar-restaurante.ps1 -Cenario 1

# Teste de carga de 60 segundos
.\testar-restaurante.ps1 -Cenario 4 -DuracaoSegundos 60
```

**Cenários Disponíveis:**

**Cenário 1: Pedidos Válidos**
- Envia 10 pedidos válidos
- Testa aprovação total
- Mostra valores processados

**Cenário 2: Restaurante Fechado** ❌
- Testa rejeição por restaurante fechado
- Validação de horário de funcionamento

**Cenário 3: Item Indisponível** ❌
- Testa rejeição por produto esgotado
- Validação de estoque

**Cenário 4: Carga Contínua** 🔄
- Envia pedidos continuamente
- Mistura casos válidos e inválidos
- Dashboard em tempo real

**Dashboard Exibido:**
- Taxa de aprovação de pedidos
- Motivos de rejeição (restaurante fechado, item indisponível)
- Valores totais validados e ticket médio

---

### 6. `testar-pagamento.ps1` 🆕

Testa o contexto de processamento de pagamentos.

**Uso:**

```powershell
# Testar TODOS os cenários
.\testar-pagamento.ps1

# Testar cenário específico (1-6)
.\testar-pagamento.ps1 -Cenario 1

# Teste de carga
.\testar-pagamento.ps1 -Cenario 6 -DuracaoSegundos 120
```

**Cenários Disponíveis:**

**Cenário 1: Pagamentos Aprovados**
- Testa cartões válidos
- Diferentes bandeiras (Visa, Master, Elo)
- Processamento normal

**Cenário 2: Cartão Recusado** ❌
- Testa cartões com saldo insuficiente
- Validação de recusa

**Cenário 3: Detecção de Fraude** 🚨
- Valores muito altos (> R$ 1000)
- Sistema de antifraude
- Pedidos bloqueados

**Cenário 4: Timeout no Pagamento** ⏱️
- Simula demora no processamento
- Testa compensação por timeout

**Cenário 5: Estorno (Chargeback)** 🔄
- Testa fluxo de estorno
- Compensação de pagamento

**Cenário 6: Carga Contínua** 🔄
- Processamento contínuo
- Mistura diferentes cenários
- Estatísticas financeiras em tempo real

**Dashboard Exibido:**
- Pagamentos aprovados vs recusados
- Motivos de recusa (cartão recusado, fraude, timeout)
- Valores processados, estornados e taxa de aprovação

---

### 7. `testar-entregador.ps1` 🆕

Testa o contexto de alocação de entregadores.

**Uso:**

```powershell
# Testar TODOS os cenários
.\testar-entregador.ps1

# Testar cenário específico (1-4)
.\testar-entregador.ps1 -Cenario 1

# Teste de carga
.\testar-entregador.ps1 -Cenario 4 -DuracaoSegundos 90
```

**Cenários Disponíveis:**

**Cenário 1: Entregadores Disponíveis**
- Alocação bem-sucedida
- Diferentes zonas de entrega
- Cálculo de frete

**Cenário 2: Entregador Indisponível** ❌
- Nenhum entregador livre
- Teste de falha por indisponibilidade

**Cenário 3: Fora de Área** 🚫
- Endereços fora da cobertura
- Validação de zona de entrega

**Cenário 4: Carga Contínua** 🔄
- Alocação contínua
- Diferentes zonas
- Dashboard em tempo real

**Dashboard Exibido:**
- Entregadores alocados vs falhas
- Motivos de falha (indisponível, fora de área)
- Valores de frete total e médio

---

### 8. `testar-notificacao.ps1` 🆕

Testa o contexto de envio de notificações aos clientes.

**Uso:**

```powershell
# Testar TODOS os cenários
.\testar-notificacao.ps1

# Testar cenário específico (1-5)
.\testar-notificacao.ps1 -Cenario 1

# Teste de carga
.\testar-notificacao.ps1 -Cenario 5 -DuracaoSegundos 60
```

**Cenários Disponíveis:**

**Cenário 1: Notificações Normais**
- Todos os canais (Email, SMS, Push)
- Envio bem-sucedido
- Múltiplos clientes

**Cenário 2: Cliente Sem Canal** ⚠️
- Cliente sem meio de contato
- Pedido aceito, notificação falha
- Teste de não-bloqueio

**Cenário 3: Tolerância a Falhas** 🛡️
- Falhas de notificação NÃO bloqueiam pedido
- Notificação é compensável
- Pedido prossegue normalmente

**Cenário 4: Múltiplos Canais** 📱
- Teste de Email, SMS e Push
- Envio balanceado entre canais
- Estatísticas por canal

**Cenário 5: Carga Contínua** 🔄
- Envio contínuo
- Alternância entre canais
- Dashboard em tempo real

**Dashboard Exibido:**
- Notificações enviadas vs falhadas
- Distribuição por canal (Email, SMS, Push)
- Taxa de sucesso de envio

---

## 📋 Resumo dos Scripts

| Script | Testa | Tempo Real | Interativo |
|--------|-------|------------|------------|
| `menu-interativo.ps1` | Tudo | ❌ | |
| `testar-casos-de-uso.ps1` | SAGA (12 casos) | ❌ | ❌ |
| `testar-restaurante.ps1` 🆕 | Restaurante | | ❌ |
| `testar-pagamento.ps1` 🆕 | Pagamento | | ❌ |
| `testar-entregador.ps1` 🆕 | Entregador | | ❌ |
| `testar-notificacao.ps1` 🆕 | Notificação | | ❌ |
| `testar-fluxo-caixa.ps1` | Fluxo Caixa | | ❌ |
| `testar-sistema-completo.ps1` | SAGA + Fluxo | | ❌ |
| `monitor-tempo-real.ps1` | Monitor | | ❌ |

---

## ⚙️ Pré-requisitos

### 1. Iniciar Infraestrutura (Docker)

```bash
cd docker
docker-compose up -d
```

Isso inicia:
- RabbitMQ (porta 5672, UI: 15672)
- PostgreSQL Lançamentos (porta 5433)
- PostgreSQL Consolidado (porta 5434)
- Redis (porta 6379)

### 2. Iniciar Serviços

**Opção A: Via Docker** (Recomendado)
```bash
docker-compose --profile saga up -d
docker-compose --profile fluxocaixa up -d
```

**Opção B: Manual** (Desenvolvimento)
```bash
# SAGA Pattern
dotnet run --project src/SagaPoc.Api
dotnet run --project src/SagaPoc.Orquestrador
dotnet run --project src/SagaPoc.ServicoRestaurante
dotnet run --project src/SagaPoc.ServicoPagamento
dotnet run --project src/SagaPoc.ServicoEntregador
dotnet run --project src/SagaPoc.ServicoNotificacao

# Fluxo de Caixa
dotnet run --project src/SagaPoc.FluxoCaixa.Api
dotnet run --project src/SagaPoc.FluxoCaixa.Lancamentos
dotnet run --project src/SagaPoc.FluxoCaixa.Consolidado
```

### 3. Verificar Conectividade

```bash
# SAGA
curl http://localhost:5000/health

# Fluxo de Caixa
curl http://localhost:5000/health

# RabbitMQ
curl http://localhost:15672
```

---

## 🎬 Fluxo de Teste Recomendado

### Para Iniciantes

1. **Execute o menu interativo**
   ```powershell
   .\menu-interativo.ps1
   ```

2. **Opção 10: Verificar Saúde dos Serviços**
   - Confirme que tudo está online

3. **Opção 1: Testar um Caso de Uso SAGA**
   - Escolha o caso 1 (Happy Path)
   - Veja o fluxo completo

4. **Opção 3: Fluxo Diário Completo**
   - Teste o Fluxo de Caixa
   - Veja lançamentos e consolidado

5. **Opção 9: Monitor em Tempo Real**
   - Deixe rodando enquanto testa
   - Acompanhe filas e estatísticas

### Para Avançados

1. **Teste de Carga**
   ```powershell
   .\testar-sistema-completo.ps1 -DuracaoSegundos 300
   ```

2. **Monitor em Janela Separada**
   ```powershell
   # Terminal 1
   .\monitor-tempo-real.ps1

   # Terminal 2
   .\testar-sistema-completo.ps1
   ```

3. **Análise de Performance**
   ```powershell
   .\testar-fluxo-caixa.ps1 -Cenario 3  # Cache performance
   ```

---

## 🐛 Troubleshooting

### Erro: "não pode ser carregado porque a execução de scripts está desabilitada"

**Windows PowerShell - Política de Execução**

```powershell
# Temporário (sessão atual)
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process

# Permanente (requer admin)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Erro: "Connection refused" ou "API não está respondendo"

1. Verifique se os serviços estão rodando
   ```bash
   docker ps
   ```

2. Verifique a porta correta
   ```bash
   netstat -an | findstr :5000
   ```

3. Teste diretamente
   ```bash
   curl http://localhost:5000/health
   ```

### Erro: "RabbitMQ não acessível"

1. Verifique se está rodando
   ```bash
   docker ps | grep rabbitmq
   ```

2. Acesse o Management UI
   ```
   http://localhost:15672
   Usuário: saga
   Senha: saga123
   ```

### Scripts estão lentos

- Reduza a duração dos testes
- Use cenários específicos ao invés de todos
- Verifique se há muitas mensagens acumuladas no RabbitMQ

---

## 📚 Mais Informações

- **[Casos de Uso SAGA](../casos-uso.md)** - Detalhes dos 12 cenários
- **[Fluxo de Caixa](../fluxo-caixa.md)** - Documentação completa
- **[Troubleshooting](../troubleshooting-fluxo-caixa.md)** - Guia de problemas
- **[Swagger UI](http://localhost:5000/swagger)** - Documentação da API

---

**Versão**: 3.0
**Data de criação**: 2026-01-07
**Última atualização**: 2026-01-15

**Changelog:**
- **v3.0** (2026-01-15): Adicionados scripts por contexto: `testar-restaurante.ps1`, `testar-pagamento.ps1`, `testar-entregador.ps1`, `testar-notificacao.ps1`
- **v2.0** (2026-01-15): Adicionados `testar-fluxo-caixa.ps1`, `testar-sistema-completo.ps1`, `monitor-tempo-real.ps1`, `menu-interativo.ps1`
- **v1.0** (2026-01-07): Script inicial `testar-casos-de-uso.ps1`
