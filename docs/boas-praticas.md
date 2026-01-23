# Aprendizados deste Projeto

O que aprendemos (e o que ainda nao sabemos) implementando esta POC.

---

## O Que Funcionou Bem

### 1. Rebus Sagas sao simples de implementar
- Handlers explicitos, faceis de entender
- Correlacao de mensagens funciona automaticamente
- Menos "magica" que MassTransit State Machine

### 2. Result Pattern evita try/catch espalhado
- Codigo fica mais previsivel
- Facil compor resultados (`Map`, `Bind`)
- Erros sao explicitos no tipo de retorno

### 3. Separar escrita e leitura (CQRS) simplifica cache
- Read Model pode cachear agressivamente
- Escrita nao e afetada por queries lentas
- Servicos podem escalar independentemente

### 4. Serilog + SEQ e muito util para debug
- Logs estruturados facilitam busca
- CorrelationId permite rastrear fluxo completo
- Query no SEQ e mais poderosa que grep

---

## O Que Deu Trabalho

### 1. Rebus Sagas perdem estado em memoria
- Tivemos que aceitar que reiniciar perde SAGAs em andamento
- Pra producao, precisaria persistir em banco

### 2. Compensacoes sao mais dificeis do que parecem
- Ordem reversa e facil de errar
- Idempotencia requer cuidado extra
- Se compensacao falha, nao ha recuperacao automatica

### 3. Cache em 3 camadas e overkill para POC
- Adiciona complexidade sem necessidade imediata
- TTL-only (sem invalidacao por evento) e simplista
- Nao sabemos se realmente atende o NFR

### 4. Observabilidade deveria ter vindo antes
- Implementamos na fase 12, mas seria mais util desde o inicio
- Debug manual e muito mais lento

---

## O Que Nao Sabemos (e Deveriamos Testar)

1. **Performance real**: Nao rodamos testes de carga. Os 50 req/s sao objetivo, nao medicao.

2. **Comportamento sob falha**: O que acontece se RabbitMQ cai no meio de uma SAGA?

3. **Dessincronizacao**: Se Write e Read Model divergirem, como detectar e corrigir?

4. **Escala horizontal**: Varias instancias do orquestrador funcionam? Ha race conditions?

5. **Mensagens perdidas**: Sem outbox pattern, mensagens podem se perder. Qual a taxa?

---

## Se Fizessemos de Novo

1. **Comecar com observabilidade** - Logs e tracing desde o dia 1
2. **Testes de carga cedo** - Validar NFRs antes de declarar "alcancado"
3. **Persistencia da SAGA** - Nao usar InMemory, mesmo em POC
4. **Cache mais simples** - Uma camada (Redis) seria suficiente para comecar
5. **Documentar limitacoes desde o inicio** - Nao deixar pra depois

---

## Referencia Rapida: Os 5 Cuidados com SAGA

1. **Idempotencia**: Verificar sempre antes de processar
2. **Compensacao**: Ordem reversa, nunca lancar excecao
3. **Timeout**: Definir para todas as operacoes
4. **CorrelationId**: Incluir em todos os logs
5. **Persistencia**: Nao confiar em memoria

---

**Ultima atualizacao**: 2026-01-21
