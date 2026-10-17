# Padroes de Design Utilizados

Este documento descreve os padroes de design implementados nesta POC.

---

## 1. SAGA Orquestrado (Orchestrated SAGA)

**O que e?**
- Padrao para transacoes distribuidas em microsservicos
- Um orquestrador central coordena o fluxo
- Cada servico executa sua operacao local

**Por que Orquestrado (vs Coreografado)?**

| Orquestrado | Coreografado |
|-------------|--------------|
| Controle centralizado | Logica espalhada |
| Facil debug e rastreamento | Dificil rastrear fluxo completo |
| Compensacoes explicitas | Cada servico conhece os outros |
| Orquestrador e ponto unico | Sem ponto unico de falha |

**Trade-off aceito**: Escolhemos orquestrado porque:
- POC educacional (mais facil de entender)
- Fluxo linear claro
- Melhor observabilidade

**O que nao sabemos**: Como isso escala com 10+ servicos participando da mesma SAGA.

---

## 2. Result Pattern

**O que e?**
Padrao funcional para encapsular sucesso/falha **sem excecoes**.

**Estrutura**:
```csharp
public class Resultado<T>
{
    public bool EhSucesso { get; }
    public bool EhFalha => !EhSucesso;
    public T Valor { get; }
    public Erro Erro { get; }

    public static Resultado<T> Sucesso(T valor) => new(valor, null);
    public static Resultado<T> Falha(string mensagem) => new(default, new Erro(mensagem));
}
```

**Beneficios**:
- Sem try/catch (codigo mais limpo)
- Erros explicitos no tipo de retorno
- Composicao fluente (`Map`, `Bind`)
- Performance (sem overhead de excecoes)

**Trade-off aceito**: Polui a assinatura dos metodos com `Resultado<T>` em vez de `T`.

---

## 3. Request/Response Pattern (Rebus)

**O que e?**
Comunicacao sincrona sobre infraestrutura assincrona usando Reply.

**Fluxo**:
```
[Orquestrador]
    | Send: ValidarPedidoRestaurante
    | (via RabbitMQ)
[Servico Restaurante]
    | Processa validacao
    | Reply: PedidoRestauranteValidado
[Orquestrador]
    | Recebe resposta e continua SAGA
```

**Por que Request/Response?**
- Saga precisa esperar resposta para decidir proximo passo
- Rebus gerencia automaticamente o roteamento da resposta

---

## 4. Compensating Transaction Pattern

**O que e?**
Desfazer operacoes ja executadas quando ocorre falha.

**Exemplo (Caso 5 - Sem Entregador)**:
```
1. Restaurante validou -> Pedido criado
2. Pagamento aprovado -> Cobranca feita
3. Entregador indisponivel -> FALHA

Compensacoes (ordem reversa):
   2. Estornar pagamento
   1. Cancelar pedido no restaurante
```

**Caracteristicas das Compensacoes**:
- **Idempotente**: Rodar 2x nao causa efeitos colaterais
- **Best-effort**: Tenta executar, mas pode falhar
- **Logged**: Todas as compensacoes sao logadas

**O que pode dar errado**: Se a compensacao falhar, o sistema fica inconsistente. Nao implementamos mecanismo de recuperacao para isso.

---

## 5. CQRS (Command Query Responsibility Segregation)

**O que e?**
Separacao entre modelo de escrita (comandos) e modelo de leitura (queries).

**Aplicacao nesta POC**:
- **Write Model**: Servico de Lancamentos (grava no PostgreSQL)
- **Read Model**: Servico de Consolidado (le do PostgreSQL com cache)

**Beneficios teoricos**:
- Escalar leitura e escrita independentemente
- Otimizar cada modelo para seu caso de uso

**O que nao implementamos**:
- Event Sourcing (poderia complementar CQRS)
- Mecanismo de reconciliacao entre modelos
- Testes de carga para validar beneficios

---

## Resumo

| Padrao | Usado para | Limitacao conhecida |
|--------|-----------|---------------------|
| SAGA Orquestrado | Coordenar transacoes distribuidas | Orquestrador e ponto unico de falha |
| Result Pattern | Tratamento de erros | Polui assinaturas de metodos |
| Request/Response | Comunicacao sincrona via mensageria | Acoplamento temporal |
| Compensacao | Desfazer operacoes em caso de falha | Nao ha recuperacao se compensacao falhar |
| CQRS | Separar escrita e leitura | Complexidade adicional, consistencia eventual |

---

**Versao**: 1.0
**Data de criacao**: 2026-01-21
