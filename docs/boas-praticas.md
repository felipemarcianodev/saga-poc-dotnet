# Aprendizados deste Projeto

O que aprendemos (e o que ainda não sabemos) implementando esta POC.

---

## O Que Funcionou Bem

### 1. Rebus Sagas são simples de implementar
- Handlers explícitos, fáceis de entender
- Correlação de mensagens configurada num lugar só (`CorrelateMessages`)
- Menos "mágica" que a state machine do MassTransit, a alternativa que comparamos
- O próprio Rebus persiste o estado no PostgreSQL, sem código de persistência nosso

### 2. Result Pattern evita try/catch espalhado
- Código fica mais previsível
- Fácil compor resultados (`Map`, `Bind`, `BindAsync`)
- Erros de negócio são explícitos no tipo de retorno

### 3. Separar escrita e leitura (CQRS) simplifica o cache
- O read model pode cachear agressivamente
- A escrita não é afetada por consultas lentas
- Os serviços podem escalar independentemente (em tese: o Consolidado ainda processa uma mensagem por vez)

### 4. Validar pelo desfecho, não pelo 202
- As APIs respondem 202 assim que aceitam o comando; isso não prova nada
- Os scripts de `docs/scripts/` conferem o log `[SAGA]` do orquestrador, o saldo consolidado e a fila `error`, e saem com código 1 se algo divergir
- O CI roda esses scripts com a stack no compose em todo PR

---

## O Que Deu Trabalho

### 1. O estado da SAGA tem um dono só
- O EF Core criava uma tabela com o mesmo nome da tabela da SAGA e outro esquema, e a SAGA quebrava
- A correção foi deixar o Rebus como único dono de `pedido_saga_data` e `pedido_saga_index`
- `MarkAsComplete()` apaga a SAGA concluída; sem uma projeção à parte, o status do pedido não tem de onde vir (hoje é mock)

### 2. Publish sem assinatura some em silêncio
- O Consolidado não assinava os eventos, e o RabbitMQ os descartava sem erro nenhum
- Mapear o evento no roteamento não adianta: o mapa só vale para `Send`
- Os testes de ponta a ponta precisam esperar as assinaturas existirem (`aguardar-stack.sh`)

### 3. Retry do EF Core e transação manual não combinam
- `EnableRetryOnFailure` recusa transação aberta à mão
- A solução foi rodar o bloco na estratégia de execução (`ExecutarEmTransacaoAsync`), que o repete inteiro, e reler o estado dentro dele

### 4. Compensações são mais difíceis do que parecem
- Ordem reversa é fácil de errar, e as confirmações chegam em qualquer ordem
- Idempotência requer cuidado extra (e a nossa é em memória)
- Se a compensação falha, não há recuperação automática

### 5. Cache em 3 camadas é excessivo para a POC
- Memória, Redis e response cache adicionam complexidade sem necessidade comprovada
- O Consolidado invalida só o Redis; as outras duas camadas deixam a leitura defasada até cerca de 60 s
- Não sabemos se o cache atende a meta de vazão, porque ela nunca foi medida

### 6. Observabilidade incompleta custa caro
- Sem instrumentação do Rebus, o orquestrador e os serviços da SAGA não aparecem no Jaeger
- Os workers não mandam log para o Seq; o diagnóstico depende de `docker logs`

### 7. Testes que não rodam escondem quebra
- O projeto de testes estava fora da solution e não compilava; `dotnet test --no-build` rodava DLLs antigas e parecia verde

---

## O Que Não Sabemos (e Deveríamos Testar)

1. **Desempenho real**: não rodamos testes de carga. Os 50 req/s do consolidado são objetivo, não medição.

2. **Comportamento sob falha**: o que acontece se o RabbitMQ cair no meio de uma SAGA?

3. **Dessincronização**: se o write model e o read model divergirem, como detectar e corrigir?

4. **Escala horizontal**: várias instâncias do orquestrador funcionam? A concorrência otimista do Rebus segura o volume?

5. **Mensagens perdidas ou duplicadas**: sem outbox, uma falha entre o commit e o envio perde a mensagem ou duplica o lançamento. Com que frequência?

---

## Se Fizéssemos de Novo

1. **Começar com observabilidade**: logs no Seq e traces atravessando o RabbitMQ desde o dia 1
2. **Testes de carga cedo**: validar as metas antes de declará-las alcançadas
3. **Projeção do status do pedido desde o início**: o estado da SAGA não serve como histórico
4. **Cache mais simples**: uma camada (Redis) seria suficiente para começar
5. **Testes de integração da SAGA** (Testcontainers com RabbitMQ e PostgreSQL), além dos scripts de ponta a ponta
6. **Documentar limitações desde o início**: não deixar para depois

---

## Referência Rápida: Os 5 Cuidados com SAGA

1. **Idempotência**: verificar sempre antes de processar
2. **Compensação**: ordem reversa, sem lançar exceção que trave a SAGA
3. **Timeout**: definir prazo para cada resposta (esta POC ainda não tem)
4. **CorrelationId**: incluir em todos os logs
5. **Persistência**: não confiar em memória, nem para o estado da SAGA nem para a deduplicação

---

**Última atualização**: 2026-10-20
