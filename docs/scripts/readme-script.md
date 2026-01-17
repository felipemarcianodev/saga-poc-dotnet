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

### 1. `testar-casos-de-uso.ps1` / `testar-casos-de-uso.sh`

Testa os 12 casos de uso do SAGA Pattern (Delivery de Comida).

**Uso:**

```powershell
# Testar TODOS os casos
.\testar-casos-de-uso.ps1

# Testar caso específico (1-12)
.\testar-casos-de-uso.ps1 5

# Alterar URL da API
.\testar-casos-de-uso.ps1 -BaseUrl "http://localhost:8080"
```

**Casos Disponíveis:**
1. Pedido Normal (Happy Path)
2. ❌ Restaurante Fechado
3. ❌ Item Indisponível
4. ❌ Cartão Recusado
5. ❌ Sem Entregador
6. ⚠️ Cliente Sem Notificação
7. ❌ Timeout no Pagamento
8. ❌ Valor Muito Alto (Fraude)
9. ❌ Endereço Fora de Área
10. Pedido VIP
11. Múltiplos Itens
12. Pedido Complexo

---

## Scripts do Fluxo de Caixa

### 2. `testar-fluxo-caixa.ps1` 🆕

Testa o sistema de Fluxo de Caixa (CQRS + Event-Driven) com visualização em tempo real.

**Uso:**

```powershell
# Testar TODOS os cenários
.\testar-fluxo-caixa.ps1

# Testar cenário específico (1-4)
.\testar-fluxo-caixa.ps1 -Cenario 1

# Alterar URL da API
.\testar-fluxo-caixa.ps1 -BaseUrl "http://localhost:5100"
```

**Cenários Disponíveis:**

**Cenário 1: Fluxo Diário Completo**
- Simula um dia completo de operações
- Créditos: Vendas pela manhã e noite
- Débitos: Compras de insumos
- Mostra consolidado ao final

**Cenário 2: Alta Frequência de Lançamentos**
- Envia 10 lançamentos em sequência rápida
- Testa throughput do sistema
- Valida se todos foram consolidados

**Cenário 3: Performance de Cache**
- Compara latência com/sem cache
- 1ª requisição: MISS (consulta banco)
- 2ª e 3ª: HIT (cache L1)
- Mostra melhoria de performance em %

**Cenário 4: Validação de Erros**
- Testa validações de dados
- Valor negativo (deve falhar)
- Comerciante vazio (deve falhar)
- Descrição muito longa (deve falhar)

**Exemplo de Saída:**

```
╔═══════════════════════════════════════════════════════════════╗
║ CENÁRIO 1: Fluxo Diário Completo (Happy Path)                ║
╚═══════════════════════════════════════════════════════════════╝

  Registrando lançamento de Credito...
  ℹ️  Comerciante: COM001
  Valor: R$ 150,00
  ℹ️  Descrição: Venda produto A
  Lançamento registrado com sucesso!

  ...

 CONSULTANDO CONSOLIDADO DO DIA

  ╭─────────────────────────────────────────╮
  │         CONSOLIDADO DIÁRIO              │
  ├─────────────────────────────────────────┤
  │ Data: 2026-01-15                        │
  │ Comerciante: COM001                     │
  ├─────────────────────────────────────────┤
  │ Créditos:     R$     700,50             │
  │ Débitos:      R$      80,00             │
  ├─────────────────────────────────────────┤
  │ Saldo Diário: R$     620,50             │
  ╰─────────────────────────────────────────╯
```

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
