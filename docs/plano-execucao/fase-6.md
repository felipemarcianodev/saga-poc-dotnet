# FASE 6: Casos de Uso e Cenários de Teste


#### 3.6.1 Casos de Uso Implementados

| # | Caso de Uso | Restaurante | Pagamento | Entregador | Resultado | Compensação |
|---|-------------|-------------|-----------|------------|-----------|-------------|
| 1 | **Pedido Normal** | `REST001` | Aprovado | Disponível | Sucesso | - |
| 2 | **Restaurante Fechado** | `REST_FECHADO` | - | - | ❌ Cancelado | - |
| 3 | **Item Indisponível** | `REST002` | - | - | ❌ Cancelado | - |
| 4 | **Pagamento Recusado** | `REST001` | Recusado | - | ❌ Cancelado | Cancelar no restaurante |
| 5 | **Sem Entregador** | `REST001` | Aprovado | Indisponível | ❌ Cancelado | ⬅️ Estornar pagamento |
| 6 | **Timeout Pagamento** | `REST001` | Timeout | - | ❌ Cancelado | Retry + compensar |
| 7 | **Pedido Premium** | `REST_VIP` | Aprovado | Prioritário | Sucesso | - |
| 8 | **Múltiplos Itens** | `REST001` | Aprovado | Disponível | Sucesso | - |
| 9 | **Endereço Longe** | `REST001` | Aprovado | Motorizado | Taxa alta | - |
| 10 | **Falha Notificação** | `REST001` | Aprovado | Disponível | ⚠️ Pedido OK | - |
| 11 | **Pedido Agendado** | `REST001` | Aprovado | Agendado | Sucesso | - |
| 12 | **Compensação Total** | `REST001` | Aprovado | Falha total | ❌ Rollback | ⬅️ Todas |

#### 3.6.2 Critérios de Aceitação
- [ ] Todos os 12 casos implementados
- [ ] Testes manuais via Swagger/Postman
- [ ] Logs mostram fluxo completo
- [ ] Compensações executadas corretamente

---

