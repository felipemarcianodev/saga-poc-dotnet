# Padrões de Design Utilizados

Este documento descreve os padrões de design implementados nesta POC.

---

## 1. SAGA Orquestrado (Orchestrated SAGA)

**O que é?**
- Padrão para transações distribuídas em microsserviços
- Um orquestrador central coordena o fluxo
- Cada serviço executa sua operação local

**Na POC**: `PedidoSaga` (Rebus) envia um comando por vez e decide o próximo passo pela resposta. O estado de cada pedido fica no PostgreSQL, persistido pelo próprio Rebus.

**Por que Orquestrado (vs Coreografado)?**

| Orquestrado | Coreografado |
|-------------|--------------|
| Controle centralizado | Lógica espalhada |
| Fácil depurar e rastrear | Difícil rastrear o fluxo completo |
| Compensações explícitas | Cada serviço conhece os eventos dos outros |
| Orquestrador é ponto único de falha | Sem ponto único de falha |

**Trade-off aceito**: escolhemos orquestrado porque:
- POC educacional (mais fácil de entender)
- Fluxo linear claro
- O estado do pedido fica num lugar só

**O que não sabemos**: como isso escala com 10+ serviços participando da mesma SAGA, nem com várias instâncias do orquestrador (o Rebus tem concorrência otimista por `Revision`, mas isso não foi testado sob carga).

---

## 2. Result Pattern

**O que é?**
Padrão funcional para representar sucesso ou falha no retorno, **sem usar exceção para regra de negócio**.

**Estrutura** (resumida de `SagaPoc.Common/ResultPattern`):
```csharp
public sealed class Resultado<T>
{
    public bool EhSucesso { get; }
    public bool EhFalha => !EhSucesso;
    public T Valor { get; }                 // lança se for falha
    public Erro Erro { get; }               // lança se for sucesso
    public IReadOnlyList<Erro> Erros { get; }

    public static Resultado<T> Sucesso(T valor);
    public static Resultado<T> Falha(Erro erro);
    public static Resultado<T> Falha(List<Erro> erros);

    public Resultado<TOut> Map<TOut>(Func<T, TOut> funcao);
    public Resultado<TOut> Bind<TOut>(Func<T, Resultado<TOut>> funcao);
    public Task<Resultado<TOut>> BindAsync<TOut>(Func<T, Task<Resultado<TOut>>> funcao);
    public TOut Match<TOut>(Func<T, TOut> sucesso, Func<Erro, TOut> falha);
}
```

`Erro` traz `Codigo`, `Mensagem` e `Tipo` (`Validacao`, `Negocio`, `NaoEncontrado`, `Timeout`, `Externo`, `Tecnico` etc.). Os serviços simulados encadeiam as validações com `Bind`/`BindAsync`, e os handlers montam a resposta com `Match`.

**Benefícios**:
- Erros de negócio explícitos no tipo de retorno
- Composição fluente (`Map`, `Bind`)
- Regra de negócio sem `try/catch`; os handlers mantêm `try/catch` só para falha inesperada, que é logada e relançada para o retry do Rebus

**Trade-off aceito**: polui a assinatura dos métodos com `Resultado<T>` em vez de `T`.

---

## 3. Request/Response Pattern (Rebus)

**O que é?**
Comando e resposta trocados por mensagens: o orquestrador envia com `Send` e o serviço responde com `Reply`. É assíncrono; a SAGA não bloqueia esperando, ela é reidratada quando a resposta chega.

**Fluxo**:
```
[Orquestrador]
    | Send: ValidarPedidoRestaurante  (fila-restaurante)
[Serviço Restaurante]
    | Processa a validação
    | Reply: PedidoRestauranteValidado  (fila-orquestrador)
[Orquestrador]
    | Correlaciona pelo CorrelacaoId e continua a SAGA
```

**Por que Request/Response?**
- A SAGA precisa da resposta para decidir o próximo passo
- O roteamento do comando vem do mapa por tipo (`Map<T>`); a resposta volta para a fila de origem, sem configuração extra

---

## 4. Publish/Subscribe (Fluxo de Caixa)

**O que é?**
O produtor publica um evento sem saber quem vai consumi-lo; cada interessado assina o tipo do evento.

**Na POC**: o `Lancamentos.Worker` publica `LancamentoCreditoRegistrado` ou `LancamentoDebitoRegistrado` (exchange `RebusTopics`), e o `FluxoCaixa.Consolidado` assina os dois ao subir.

**O que pode dar errado**: sem a assinatura, o RabbitMQ descarta o evento em silêncio. Mapear o evento no roteamento não resolve, porque o mapa só vale para `Send`. Por isso o `aguardar-stack.sh` espera as assinaturas antes dos testes.

---

## 5. Compensating Transaction Pattern

**O que é?**
Desfazer operações já executadas quando uma etapa seguinte falha.

**Exemplo (Caso 5 - Sem Entregador)**:
```
1. Restaurante validou -> Pedido criado
2. Pagamento aprovado -> Cobrança feita
3. Entregador indisponível -> FALHA

Compensações (ordem reversa):
   2. Estornar pagamento
   1. Cancelar pedido no restaurante
```

Os dois comandos de compensação saem no mesmo handler, na ordem reversa, mas são processados em paralelo por serviços diferentes. A SAGA só termina quando as duas confirmações chegam, em qualquer ordem.

**Características das compensações**:
- **Idempotente**: o estorno é deduplicado por chave (`estorno:{TransacaoId}`) num repositório em memória
- **Best-effort**: tenta executar, mas pode falhar
- **Logged**: todas as compensações são logadas com o `CorrelacaoId`

**O que pode dar errado**:
- Se a compensação falhar e a confirmação nunca chegar, a SAGA fica parada em `ExecutandoCompensacao`. Não há timeout nem recuperação automática.
- A deduplicação em memória se perde quando o serviço reinicia e não vale entre instâncias.

---

## 6. CQRS (Command Query Responsibility Segregation)

**O que é?**
Separação entre modelo de escrita (comandos) e modelo de leitura (consultas).

**Aplicação nesta POC**:
- **Write model**: `Lancamentos.Api` (aceita o comando) e `Lancamentos.Worker` (grava em `fluxocaixa_lancamentos` e publica o evento)
- **Read model**: `FluxoCaixa.Consolidado` (aplica os eventos no saldo diário, em `fluxocaixa_consolidado`) e `FluxoCaixa.Api` (consulta com cache em memória e Redis)

**Benefícios teóricos**:
- Escalar leitura e escrita independentemente
- Otimizar cada modelo para seu caso de uso

**O que não implementamos**:
- Event Sourcing (poderia complementar o CQRS)
- Mecanismo de reconciliação entre os modelos
- Testes de carga para validar os benefícios
- Escala do Consolidado: ele processa uma mensagem por vez (`MaxParallelism = 1`), porque a linha do dia não tem controle de concorrência

---

## 7. Unit of Work

**O que é?**
Agrupar as alterações de uma operação numa única transação, gravadas juntas ou descartadas juntas.

**Na POC** (fluxo de caixa): `IUnitOfWork.ExecutarEmTransacaoAsync` abre a transação dentro da estratégia de execução do EF Core. Os DbContexts usam `EnableRetryOnFailure`, que recusa transação aberta à mão; com a estratégia, uma falha transitória repete o bloco inteiro.

**Cuidados**:
- Dentro do bloco, ler o estado de novo a cada execução (o bloco pode rodar mais de uma vez; o `ChangeTracker` é limpo no início de cada tentativa)
- Não chamar `Update` em entidade recém-adicionada: vira UPDATE de linha inexistente

**O que pode dar errado**: a transação cobre só o banco. O evento é publicado depois do commit, sem outbox: se o envio falhar, o Rebus repete a mensagem e o worker grava o lançamento de novo, com outro `Id`.

---

## Resumo

| Padrão | Usado para | Limitação conhecida |
|--------|-----------|---------------------|
| SAGA Orquestrado | Coordenar transações distribuídas | Orquestrador é ponto único de falha; SAGA concluída some do banco |
| Result Pattern | Tratamento de erros de negócio | Polui assinaturas de métodos |
| Request/Response | Comando e resposta via mensageria | Acoplamento temporal; sem timeout de resposta |
| Publish/Subscribe | Eventos do fluxo de caixa | Evento sem assinatura é descartado em silêncio |
| Compensação | Desfazer operações em caso de falha | Não há recuperação se a compensação falhar |
| CQRS | Separar escrita e leitura | Consistência eventual; leitura defasada até cerca de 60 s |
| Unit of Work | Transação no fluxo de caixa | Sem outbox; não cobre a publicação do evento |

---

**Última atualização**: 2026-10-20
