# Arquitetura - POC SAGA Pattern

Este documento descreve a estrutura macro da POC: serviços, comunicação, persistência, deploy e observabilidade. Os padrões internos (SAGA, CQRS, Result Pattern, Unit of Work) estão em [design/padroes-utilizados.md](design/padroes-utilizados.md), e as escolhas de tecnologia em [design/escolhas-tecnicas.md](design/escolhas-tecnicas.md).

---

## Visão Geral da Arquitetura

### Arquitetura de Alto Nível

```mermaid
flowchart TB
    subgraph entrada["CAMADA DE ENTRADA"]
        api["<b>SagaPoc.Api</b><br/>:5000<br/>• POST /api/pedidos<br/>• GET /api/pedidos/{id}/status (mock)<br/>• GET /health"]
        apilanc["<b>Lancamentos.Api</b><br/>:5200 (escrita)<br/>• POST /api/lancamentos<br/>• GET /api/lancamentos"]
        apifc["<b>FluxoCaixa.Api</b><br/>:5100 (consulta)<br/>• GET /api/consolidado/..."]
    end

    subgraph orquestracao["CAMADA DE ORQUESTRAÇÃO"]
        orch["<b>SagaPoc.Orquestrador</b><br/>PedidoSaga (Rebus)"]
    end

    subgraph bus["RABBITMQ :5672 (via Rebus)"]
        direction LR
        qo["fila-orquestrador"]
        q1["fila-restaurante"]
        q2["fila-pagamento"]
        q3["fila-entregador"]
        q4["fila-notificacao"]
        q5["fluxocaixa-lancamentos"]
        topics["RebusTopics<br/>(Publish/Subscribe)"]
        q6["fluxocaixa-consolidado"]
    end

    subgraph servicos["SERVIÇOS DA SAGA (simulados)"]
        direction LR
        rest["Restaurante"]
        pag["Pagamento"]
        ent["Entregador"]
        notif["Notificação"]
    end

    subgraph fluxocaixa["FLUXO DE CAIXA (CQRS)"]
        worker["<b>Lancamentos.Worker</b><br/>grava e publica eventos"]
        consolidado["<b>FluxoCaixa.Consolidado</b><br/>aplica os eventos no saldo do dia"]
    end

    subgraph databases["DADOS"]
        pg1[(PostgreSQL :5432<br/>estado da SAGA)]
        pg2[(PostgreSQL :5433<br/>lançamentos)]
        pg3[(PostgreSQL :5434<br/>consolidado)]
        redis[(Redis :6379)]
    end

    api -->|"Send: IniciarPedido"| qo
    qo --> orch
    orch -->|Send| q1 & q2 & q3 & q4
    q1 --> rest
    q2 --> pag
    q3 --> ent
    q4 --> notif
    rest & pag & ent & notif -->|Reply| qo
    orch --> pg1

    apilanc -->|"Send: RegistrarLancamento"| q5
    q5 --> worker
    worker --> pg2
    worker -->|Publish| topics
    topics --> q6
    q6 --> consolidado
    consolidado --> pg3
    consolidado -->|apaga a chave do dia| redis
    apifc --> pg3
    apifc --> redis
```

Toda a comunicação entre serviços passa pelo RabbitMQ, via Rebus. Nenhum serviço chama outro por HTTP.

---

## Componentes Principais

### 1. **SagaPoc.Api** (Camada de Entrada)

**Responsabilidade**: Ponto de entrada HTTP dos pedidos.

**Tecnologias**:
- ASP.NET Core (.NET 10)
- Swagger (na raiz, `http://localhost:5000`, só no ambiente Development, que é o do compose)
- Rebus como cliente de mão única (`UseRabbitMqAsOneWayClient`): só envia, não consome fila

**Endpoints**:
```
POST   /api/pedidos              # Cria o pedido e inicia a SAGA (202 Accepted)
GET    /api/pedidos/{id}/status  # MOCK: sempre responde "EmProcessamento"
GET    /health                   # Health check
```

**Fluxo**:
1. Recebe `POST /api/pedidos`
2. Valida o payload (DataAnnotations, 400 com a lista de erros): campos obrigatórios, pelo menos um item, quantidade e preço maiores que zero, endereço com pelo menos 10 caracteres e forma de pagamento `CREDITO`, `DEBITO`, `PIX` ou `DINHEIRO` (sem diferenciar maiúsculas). As regras de negócio (restaurante, limite de itens, valor máximo, área de entrega) continuam nos serviços
3. Gera um `CorrelacaoId` novo (Guid), que vira o `Id` da SAGA
4. Envia `IniciarPedido` para a `fila-orquestrador`
5. Retorna **202 Accepted** com o identificador do pedido

**Características e limitações**:
- Stateless: não guarda nada do pedido.
- O 202 só diz que o pedido foi aceito. O desfecho aparece no log `[SAGA]` do orquestrador.
- **Não é idempotente**: cada `POST` gera um `CorrelacaoId` novo, então reenviar o mesmo payload cria outra SAGA.
- O status é mock porque a SAGA concluída é removida do banco (ver [Estado da SAGA](#estado-da-saga)).

---

### 2. **SagaPoc.Orquestrador** (Camada de Orquestração)

**Responsabilidade**: Coordenar a SAGA e guardar o estado de cada pedido em andamento.

**Componentes**:
- `PedidoSaga`: `Saga<PedidoSagaData>` com um handler por mensagem (`IAmInitiatedBy<IniciarPedido>` e `IHandleMessages<...>` para as respostas)
- `PedidoSagaData`: estado da SAGA (`ISagaData`), persistido pelo Rebus no PostgreSQL

O estado é um texto em `EstadoAtual`, não uma máquina de estados declarativa. O diagrama abaixo reflete os valores e as transições do código:

```mermaid
stateDiagram-v2
    [*] --> ValidandoRestaurante: IniciarPedido

    ValidandoRestaurante --> ProcessandoPagamento: PedidoRestauranteValidado (válido)
    ValidandoRestaurante --> Cancelado: PedidoRestauranteValidado (inválido)

    ProcessandoPagamento --> AlocandoEntregador: PagamentoProcessado (sucesso)
    ProcessandoPagamento --> ExecutandoCompensacao: PagamentoProcessado (falha)

    AlocandoEntregador --> NotificandoCliente: EntregadorAlocado (alocado)
    AlocandoEntregador --> ExecutandoCompensacao: EntregadorAlocado (indisponível)

    NotificandoCliente --> Concluido: NotificacaoEnviada (enviada ou não)

    ExecutandoCompensacao --> Cancelado: todas as confirmações recebidas

    Concluido --> [*]
    Cancelado --> [*]

    note right of ExecutandoCompensacao
        Falha no pagamento: CancelarPedidoRestaurante
        Falha no entregador: EstornarPagamento
        e CancelarPedidoRestaurante
    end note
```

**Mensagens tratadas**:
- `IniciarPedido`: inicia a SAGA (ignorada se a SAGA já existir, por `IsNew`)
- `PedidoRestauranteValidado`, `PagamentoProcessado`, `EntregadorAlocado`, `NotificacaoEnviada`: respostas do fluxo principal
- `PedidoRestauranteCancelado`, `PagamentoEstornado`, `EntregadorLiberado`: respostas das compensações, com o campo `Sucesso` conferido
- `PrazoPedidoExpirado`, `PrazoCompensacaoExpirado`: prazos que a SAGA agenda para si mesma (`DeferLocal`), guardados na tabela `rebus_timeouts` do PostgreSQL até a hora da entrega

**Compensação**: as compensações são enviadas na ordem reversa (entregador, pagamento, restaurante), mas saem no mesmo handler e são processadas em paralelo por serviços diferentes; as respostas podem chegar em qualquer ordem. Cada passo fica em `PassosPendentes` até a resposta. Resposta com `Sucesso = false` gera nova tentativa depois de 2 s, até 3 tentativas; esgotadas, o passo vai para `PassosComFalha`. Quando não resta passo pendente, a SAGA termina como `Cancelado` (tudo compensado) ou `CompensacaoFalhou` (algum passo sem compensação, com log de erro pedindo intervenção manual).

**Prazos** (seção `Saga` do `appsettings.json`):
- `PrazoPedidoSegundos` (30): se o pedido não chegar à notificação nesse tempo, a SAGA compensa o que já foi feito. Se o entregador já foi alocado, o pedido está confirmado e a SAGA conclui, mesmo sem resposta da notificação.
- `PrazoCompensacaoSegundos` (30): passos que não responderam nesse tempo contam como falha.
- O prazo do pedido também é conferido na chegada de cada resposta de sucesso: se já passou, a SAGA registra o recurso que a resposta trouxe e compensa, em vez de seguir para a próxima etapa. Isso cobre a entrega atrasada da mensagem adiada, observada com a máquina carregada (o prazo de 30 s não chegou antes de uma resposta de 40 s).
- Com o prazo esgotado, a SAGA compensa também o passo que não respondeu, sem o identificador do recurso: `EstornarPagamento` e `LiberarEntregador` vão com `TransacaoId`/`EntregadorId` nulos, e `CancelarPedidoRestaurante` vai com o próprio `CorrelacaoId`. Cada serviço guarda, por pedido, o recurso que criou e se o pedido foi cancelado (`IRegistroCancelamentos`): compensa o que já tiver feito, recusa a operação que chegar depois e desfaz a que terminar depois do cancelamento. Por isso uma resposta descartada pelo Rebus (a SAGA já terminou) não deixa cobrança, entregador ou pedido para trás.
- O registro e a operação conferem um ao outro em ordem inversa (a operação registra o recurso e depois confere o cancelamento; a compensação marca o cancelamento e depois procura o recurso), então, mesmo em paralelo, pelo menos um dos dois lados desfaz o trabalho.

**Observações sobre o código atual**:
- "Timeout no pagamento" (`CLI_TIMEOUT`) é uma resposta de falha do serviço simulado. O prazo da SAGA é testado pelo restaurante `REST_LENTO`, que demora 40 s para responder.
- A falha no restaurante não compensa nada: a SAGA notifica o cliente e termina como `Cancelado`.
- Respostas fora da etapa atual (repetidas ou fora de ordem) são ignoradas com aviso no log.

**Por que Rebus Sagas?**
- Controle centralizado do fluxo via handlers
- Implementação explícita e clara
- Suporte nativo a correlação de mensagens (`CorrelateMessages`, pelo `CorrelacaoId`)
- Compensações manuais, porém explícitas

---

### 3. **Serviços da SAGA**

Cada serviço é um Worker Service que consome a própria fila e responde ao orquestrador com `Reply`. Todos são **simulados**: decidem pelo conteúdo do pedido e guardam dados em memória. As regras abaixo são as do código; os cenários que as exercitam estão em [casos-uso.md](casos-uso.md).

#### 3.1 **SagaPoc.ServicoRestaurante**

**Handlers**: `ValidarPedidoRestauranteHandler` e `CancelarPedidoRestauranteHandler` (compensação).

**Validação** (`ServicoRestaurante.ValidarPedidoAsync`, encadeada com Result Pattern):

```mermaid
flowchart TD
    A[ValidarPedidoRestaurante] --> B{Restaurante}
    B -->|REST_FECHADO, REST_INATIVO, REST_INVALIDO<br/>ou ID que não começa com REST| X[Reply: Valido = false]
    B -->|ok| C{Itens}
    C -->|ProdutoId INDISPONIVEL, quantidade > 10<br/>ou preço <= 0: junta todos os erros| X
    C -->|ok| D{Valor total >= R$ 10,00?}
    D -->|não: VALOR_MINIMO| X
    D -->|sim| E[Tempo de preparo: 15 min por item<br/>REST_VIP: metade]
    E --> F[Reply: Valido = true, ValorTotal, TempoPreparoMinutos, PedidoId]
```

**Compensação**: `CancelarPedidoRestaurante` registra o cancelamento num dicionário em memória e responde `PedidoRestauranteCancelado`.

---

#### 3.2 **SagaPoc.ServicoPagamento**

**Handlers**: `ProcessarPagamentoHandler` e `EstornarPagamentoHandler` (compensação).

O orquestrador cobra o valor do pedido mais 15% de taxa de entrega.

**Regras**:
- Valor <= 0 ou acima de R$ 10.000,00: recusado
- Forma de pagamento fora de `CREDITO`, `DEBITO`, `PIX`, `DINHEIRO`: recusado
- `CLI_CARTAO_RECUSADO`, `CLI_SALDO_INSUFICIENTE`, `CLI_TIMEOUT`, `CLI_ERRO_GATEWAY`: recusado (cada um com seu código de erro)
- Demais casos: aprovado, com `TransacaoId` no formato `TXN_...`

**Estorno**: idempotente em duas camadas, ambas em memória (perdidas quando o serviço reinicia): a chave `estorno:{TransacaoId}` no `IRepositorioIdempotencia` e a marca de estornado na transação.

Existe uma variante com Polly (`ServicoPagamentoComPolly`: retry, circuit breaker e timeout), mas ela **não está registrada** no `Program.cs`; o serviço em uso é o `ServicoPagamento`.

---

#### 3.3 **SagaPoc.ServicoEntregador**

**Handlers**: `AlocarEntregadorHandler` e `LiberarEntregadorHandler` (compensação, enviada quando o prazo do pedido esgota durante a alocação ou quando a alocação chega durante a compensação).

**Regras**:
- Taxa de entrega negativa ou acima de R$ 100,00: recusado
- Endereço vazio, com menos de 10 caracteres ou contendo `LONGE`/`DISTANTE`: recusado
- Horário de pico (12h a 14h59 UTC): 30% das alocações são recusadas. Desliga com `Simulacao:HorarioPico:Habilitado=false` (no compose, `SIMULAR_HORARIO_PICO=false`; o CI usa assim)
- Pool de 10 entregadores; sem nenhum livre, recusa
- Tempo estimado: distância simulada × 3 min + 5 a 15 min; `REST_VIP` reduz 30%
- O entregador volta ao pool quando a entrega simulada termina (1 minuto estimado = 1 segundo)

---

#### 3.4 **SagaPoc.ServicoNotificacao**

**Handler**: `NotificarClienteHandler`.

**Regras**:
- `CLI_SEM_NOTIFICACAO` e `CLI_INVALIDO`: falha
- 5% de falha aleatória em qualquer cliente
- Os "canais" (e-mail, SMS, push) existem só no log

**Tratamento de falha**: o handler sempre responde `NotificacaoEnviada` (com `Enviada = false` em caso de falha) e não relança exceção. Notificação é best effort: o pedido termina como `Concluido` mesmo sem ela.

---

### 4. **Fluxo de Caixa** (CQRS)

Contexto separado da SAGA, com escrita e leitura em serviços e bancos diferentes. Regras de negócio e diagramas em [fluxo-caixa.md](fluxo-caixa.md) e [diagramas-fluxo-caixa.md](diagramas-fluxo-caixa.md).

**Projetos**:

| Projeto | Papel |
|---|---|
| `SagaPoc.ServicoLancamentos/SagaPoc.Lancamentos.Api` | API de escrita (:5200): aceita o lançamento e envia `RegistrarLancamento` (202) |
| `SagaPoc.ServicoLancamentos/SagaPoc.Lancamentos.Worker` | Consome `fluxocaixa-lancamentos`, grava o lançamento e publica o evento |
| `SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain` | Agregados (`Lancamento`, `ConsolidadoDiario`), comandos, eventos, repositórios |
| `SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Application` | Serviços de aplicação (consulta do consolidado com cache) |
| `SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure` | DbContexts, repositórios, Unit of Work, `RedisCacheService` |
| `SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado` | Worker que assina os eventos e atualiza o saldo diário |
| `SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api` | API de consulta (:5100) |

**Fluxo**:
1. `POST /api/lancamentos` (:5200) envia `RegistrarLancamento` para `fluxocaixa-lancamentos` e responde 202.
2. O worker grava o lançamento em `fluxocaixa_lancamentos` (:5433) dentro de `IUnitOfWork.ExecutarEmTransacaoAsync` e publica `LancamentoCreditoRegistrado` ou `LancamentoDebitoRegistrado` (Publish no exchange `RebusTopics`).
3. O Consolidado assina os dois eventos ao subir (`onCreated`), recebe-os em `fluxocaixa-consolidado`, atualiza a linha do dia em `fluxocaixa_consolidado` (:5434) e apaga a chave do dia no Redis.
4. A API de consulta lê na ordem: cache em memória (1 min), Redis (5 min), banco. O endpoint do dia ainda tem response cache de 60 s.

**Endpoints**:
```
# API de lançamentos (:5200)
POST   /api/lancamentos                          # Registrar lançamento (202)
GET    /api/lancamentos/{id}                     # Lançamento por ID
GET    /api/lancamentos?comerciante=...          # Lançamentos por período

# API de consulta (:5100)
GET    /api/consolidado/{comerciante}/{data}     # Consolidado do dia
GET    /api/consolidado/{comerciante}/periodo    # Consolidado de um período
GET    /health
```

**Limitações**:
- **Leitura defasada até cerca de 60 s**: o Consolidado apaga a chave do Redis, mas o cache em memória e o response cache da API de consulta não são invalidados.
- **Consolidado com `MaxParallelism = 1`**: a linha do dia não tem controle de concorrência, e dois eventos em paralelo perderiam uma atualização. Escalar exige concorrência otimista ou UPDATE atômico.
- **Worker de lançamentos não é idempotente**: cada tentativa gera um `Id` novo; se o envio do evento falhar depois do commit, a repetição grava o lançamento de novo.
- **Sem medição de desempenho**: as metas de vazão e latência dos ADRs ([001](decisoes-tecnicas/001-cqrs.md), [002](decisoes-tecnicas/002-cache.md)) são objetivos, não resultados.

---

### 5. **BuildingBlocks** (Camada Compartilhada)

| Projeto | Conteúdo |
|---|---|
| `SagaPoc.Common` | Result Pattern (`Resultado<T>`, `Erro`), contratos de mensagem da SAGA, modelos compartilhados |
| `SagaPoc.Infrastructure.Core` | `IRepositorioIdempotencia` e a implementação em memória |
| `SagaPoc.Infrastructure` | Só referências de pacote (EF Core + Npgsql); não tem código próprio |
| `SagaPoc.Observability` | `AddSagaOpenTelemetry`/`AddSagaOpenTelemetryForHost` e `UseCustomSerilog` |
| `WebHost` | Extensões de Rebus, banco (com `EnableRetryOnFailure` e migração), cache, Swagger e tratamento global de exceções |

---

## Comunicação e Mensageria

### RabbitMQ (via Rebus)

Configuração do orquestrador (resumida de `SagaPoc.Orquestrador/Program.cs`):

```csharp
builder.Services.AddRebus((configure, provider) => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq(
        $"amqp://{username}:{password}@{host}",
        "fila-orquestrador"))
    .Sagas(s => s.StoreInPostgres(
        connectionString,
        "pedido_saga_data",
        "pedido_saga_index",
        automaticallyCreateTables: true))
    .Routing(r => r.TypeBased()
        .Map<ValidarPedidoRestaurante>("fila-restaurante")
        .Map<ProcessarPagamento>("fila-pagamento")
        // ... demais comandos e compensações
        .Map<LiberarEntregador>("fila-entregador"))
    .Options(o =>
    {
        o.SetNumberOfWorkers(1);
        o.SetMaxParallelism(10);
    })
);

builder.Services.AutoRegisterHandlersFromAssemblyOf<PedidoSaga>();
```

**Filas** (criadas pelo Rebus ao subir cada serviço):
```
fila-orquestrador         Respostas para a SAGA
fila-restaurante          Comandos do serviço de restaurante
fila-pagamento            Comandos do serviço de pagamento
fila-entregador           Comandos do serviço de entregador
fila-notificacao          Comandos do serviço de notificação
fluxocaixa-lancamentos    RegistrarLancamento
fluxocaixa-consolidado    Eventos assinados pelo Consolidado (ligada ao exchange RebusTopics)
error                     Mensagens que esgotaram as tentativas
```

**Send x Publish**: a SAGA usa `Send`/`Reply`, e o roteamento por tipo (`Map<T>`) define a fila de destino. Os eventos do fluxo de caixa usam `Publish`; o mapa de roteamento não vale para eles. Sem a assinatura do Consolidado, o RabbitMQ descarta os eventos em silêncio, por isso o `aguardar-stack.sh` espera as duas assinaturas antes dos testes.

**Fila `error`**: o Rebus usa a estratégia de retry padrão (5 tentativas imediatas, sem espera entre elas) e depois move a mensagem para a fila `error`. Não há retry de segundo nível configurado. Mensagem na fila `error` é sempre bug.

---

### Contratos de Mensagem

Os contratos são `record`s em `SagaPoc.Common/Mensagens` (comandos e respostas da SAGA) e em `SagaPoc.FluxoCaixa.Domain` (comandos e eventos do fluxo de caixa).

**Comando**:
```csharp
public record ValidarPedidoRestaurante(
    Guid CorrelacaoId,
    string RestauranteId,
    List<ItemPedido> Itens
);
```

**Resposta**:
```csharp
public record PedidoRestauranteValidado(
    Guid CorrelacaoId,
    bool Valido,
    decimal ValorTotal,
    int TempoPreparoMinutos,
    Guid? PedidoId,
    string? MotivoRejeicao
);
```

**Por que records?**
- Imutáveis por padrão
- Igualdade por valor (útil em testes)
- Sintaxe concisa
- Serialização JSON sem configuração extra

---

## Persistência e Estado

### Estado da SAGA

O Rebus persiste o `PedidoSagaData` no PostgreSQL da SAGA (:5432, banco `sagapoc`), em duas tabelas que ele mesmo cria e mantém:

| Tabela | Conteúdo |
|---|---|
| `pedido_saga_data` | `id`, `revision` e `data` (o `PedidoSagaData` serializado em JSON, em `bytea`) |
| `pedido_saga_index` | Índice de correlação usado para localizar a SAGA a partir da mensagem |

Regras e consequências:
- **Nenhum outro componente cria ou altera essas tabelas** (EF Core, migration, SQL manual). Uma tabela com o mesmo nome criada pelo EF, com outro esquema, já quebrou a SAGA neste projeto.
- `Revision` é o controle de concorrência otimista do Rebus: duas mensagens da mesma SAGA processadas ao mesmo tempo geram conflito, e a mensagem perdedora é repetida.
- **SAGA concluída é removida**: `MarkAsComplete()` apaga a linha. O desfecho final não fica persistido em lugar nenhum, e é por isso que `GET /api/pedidos/{id}/status` ainda é mock.
- O estado não passa pelo EF Core; o orquestrador não tem DbContext.

Campos principais do `PedidoSagaData`:
```csharp
public class PedidoSagaData : ISagaData
{
    public Guid Id { get; set; }              // CorrelacaoId do pedido
    public int Revision { get; set; }         // Concorrência otimista (Rebus)
    public string EstadoAtual { get; set; }

    // Dados do pedido
    public string ClienteId { get; set; }
    public string RestauranteId { get; set; }
    public decimal ValorTotal { get; set; }
    public string EnderecoEntrega { get; set; }
    public string FormaPagamento { get; set; }

    // Controle de compensação
    public string? TransacaoId { get; set; }  // Para estorno
    public string? EntregadorId { get; set; } // Para liberação
    public bool RestauranteValidado { get; set; }
    public bool PagamentoProcessado { get; set; }
    public bool EntregadorAlocado { get; set; }
    public bool EmCompensacao { get; set; }
    public List<string> PassosCompensados { get; set; }

    // Timestamps
    public DateTime DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }
}
```

### Fluxo de Caixa

| Banco | Porta | Dono da migration |
|---|---|---|
| `fluxocaixa_lancamentos` | 5433 | `Lancamentos.Worker` |
| `fluxocaixa_consolidado` | 5434 | `FluxoCaixa.Consolidado` |

As APIs não migram, para não disputarem a mesma migration ao subir. Os DbContexts usam `EnableRetryOnFailure`, que recusa transação aberta à mão; por isso a transação passa por `IUnitOfWork.ExecutarEmTransacaoAsync`, que repete o bloco inteiro em falha transitória.

---

## Confiabilidade e Resiliência

O que existe hoje, e o que não existe:

| Mecanismo | Situação |
|---|---|
| Retry de mensagens | Padrão do Rebus: 5 tentativas imediatas e depois a fila `error` |
| Retry de banco | `EnableRetryOnFailure` do EF Core no fluxo de caixa (até 5 tentativas, espera máxima de 30 s) |
| Idempotência | `IsNew` no início da SAGA; estorno e liberação deduplicados em memória; worker de lançamentos **sem** idempotência |
| Circuit breaker | Não está em uso (a variante com Polly do pagamento não é registrada) |
| Prazos da SAGA | `PrazoPedidoExpirado` e `PrazoCompensacaoExpirado` (30 s cada, configuráveis), mensagens adiadas guardadas em `rebus_timeouts`; ver a seção do orquestrador |
| Outbox | Não existe: se um serviço cair entre gravar no banco e enviar a mensagem, estado e mensageria divergem |

### Idempotência das compensações

O estorno consulta o repositório de idempotência antes de processar e responde sucesso se a chave já existir:

```csharp
var chaveIdempotencia = $"estorno:{mensagem.TransacaoId}";

if (await _idempotencia.JaProcessadoAsync(chaveIdempotencia))
{
    await _bus.Reply(new PagamentoEstornado(mensagem.CorrelacaoId, Sucesso: true, TransacaoId: mensagem.TransacaoId));
    return;
}
```

O repositório é `RepositorioIdempotenciaInMemory`: a proteção vale só enquanto o processo estiver no ar e só para uma instância do serviço.

---

## Observabilidade

### 1. Logs (Serilog + Seq)

**Seq**: http://localhost:5342 (admin/admin123).

Todos os serviços (APIs e workers) passam pelo mesmo `UseCustomSerilog`, que define os destinos no código; o `appsettings.json` só ajusta os níveis (`Serilog:MinimumLevel`).

| Destino | Quando |
|---|---|
| Console (`docker logs`) | sempre |
| Seq | sempre (`Seq:Url`; a ingestão não exige chave) |
| Arquivo `logs/{Serviço}-{data}.log` | só em build Debug (`#if DEBUG`), um arquivo por serviço e por dia, 7 dias guardados |

Para ter os arquivos com a stack no Docker, suba com `BUILD_CONFIGURATION=Debug`: os Dockerfiles publicam nessa configuração, e o compose monta `/app/logs` de cada serviço em `logs/` na raiz do repositório (fora do git). Com isso, `tail -F logs/*.log` acompanha a stack inteira, e o histórico não some quando o container é recriado.

```bash
BUILD_CONFIGURATION=Debug docker compose -f docker/docker-compose.yml up -d --build
tail -F logs/*.log | grep -E "\[(WRN|ERR)\]"
```

O Serilog acrescenta `Application`, `Environment`, `MachineName`, `ThreadId` e `ProcessId`. Os identificadores de negócio (`PedidoId`, `CorrelacaoId`, `LancamentoId`) entram como propriedades estruturadas da mensagem.

**Consultas úteis no Seq**:
```
Application = "SagaPoc.Api"
@Level = "Error"
@MessageTemplate like '%Lancamento%'
```

### 2. Traces (OpenTelemetry + Jaeger)

**Jaeger UI**: http://localhost:16686.

`SagaPoc.Observability/OpenTelemetryExtensions.cs` configura, em todos os serviços que o chamam:
- instrumentação de ASP.NET Core, HttpClient e EF Core;
- `AddSource("SagaPoc.*")` (hoje nenhum código cria `ActivitySource` próprio);
- exportação por OTLP gRPC para `Otlp:Endpoint` (no compose, `http://jaeger:4317`), ligada por `Otlp:Enabled`.

Não há exportador de métricas nem endpoint `/metrics`.

**O que aparece no Jaeger hoje**: só `SagaPoc.Api`, `Lancamentos.Api` e `Lancamentos.Worker` (este pelos comandos do EF Core).
- O orquestrador e os serviços da SAGA não geram span: não recebem HTTP, não usam EF Core, e o Rebus não tem instrumentação configurada.
- Sem instrumentação do Rebus, o contexto do trace não atravessa o RabbitMQ: o span da API e os do worker não ficam no mesmo trace.
- A `FluxoCaixa.Api` não recebe `Otlp__Endpoint` no compose e tenta exportar para `localhost:4317`, que não existe dentro do container.
- O Consolidado não chama `AddSagaOpenTelemetryForHost`.
- As portas 4317/4318 do Jaeger não são publicadas no host; um serviço rodando fora do Docker não exporta traces sem publicar a porta.

Configuração (`appsettings.json` ou variáveis de ambiente `Otlp__Enabled` e `Otlp__Endpoint`):
```json
{
  "Otlp": {
    "Enabled": true,
    "Endpoint": "http://jaeger:4317"
  }
}
```

---

## Deploy (Docker Compose)

Toda a stack está em `docker/docker-compose.yml`:

```bash
docker compose -f docker/docker-compose.yml up -d --build
bash docs/scripts/aguardar-stack.sh
```

| Grupo | Serviços (container) |
|---|---|
| Infraestrutura | `saga-rabbitmq` (RabbitMQ 3.13), `saga-postgres`, `fluxocaixa-postgres-lancamentos`, `fluxocaixa-postgres-consolidado` (PostgreSQL 16), `fluxocaixa-redis` (Redis 7) |
| Observabilidade | `saga-seq`, `saga-jaeger` |
| SAGA | `saga-api`, `saga-orquestrador`, `saga-servico-restaurante`, `saga-servico-pagamento`, `saga-servico-entregador`, `saga-servico-notificacao` |
| Fluxo de caixa | `lancamentos-api`, `lancamentos-worker`, `fluxocaixa-consolidado`, `fluxocaixa-api` |

**Portas no host**:

| Componente | Porta | Observação |
|---|---|---|
| API de pedidos | 5000 | Swagger na raiz |
| API de consulta do fluxo de caixa | 5100 | `/swagger` |
| API de lançamentos | 5200 | `/swagger` |
| RabbitMQ | 5672 / 15672 | management em 15672 (saga/saga123) |
| PostgreSQL SAGA / lançamentos / consolidado | 5432 / 5433 / 5434 | bancos `sagapoc`, `fluxocaixa_lancamentos`, `fluxocaixa_consolidado` |
| Redis | 6379 | |
| Seq | 5342 | admin/admin123 |
| Jaeger | 16686 | OTLP (4317) só na rede interna |

Os Dockerfiles copiam `src/` inteiro e o `Directory.Build.props` (que define `net10.0`), com imagens `sdk:10.0` e `aspnet:10.0`. As connection strings do compose usam `GSS Encryption Mode=Disable`, porque a imagem `aspnet:10.0` não tem a biblioteca de Kerberos que o Npgsql tenta usar ao conectar. Operação da stack e consultas SQL em [docker/README.md](../docker/README.md).

---

## Segurança

**Hoje**: as APIs não têm autenticação, e as credenciais (RabbitMQ, PostgreSQL, Seq) estão fixas no compose. É aceitável numa POC local, não fora dela.

**Para produção**:
1. **Autenticação no RabbitMQ**: um usuário por serviço, com permissão só nas próprias filas, e AMQPS (AMQP sobre TLS):
   ```csharp
   .Transport(t => t.UseRabbitMq(
       $"amqps://{username}:{password}@rabbitmq.exemplo.com",
       "fila-servico"))
   ```
2. **Segredos fora do compose**: variáveis injetadas por um cofre (Key Vault, Secrets Manager etc.).
3. **Criptografia em trânsito**: HTTPS nas APIs, TLS no RabbitMQ, PostgreSQL e Redis. Hoje o compose desliga o redirecionamento HTTPS (`Https__Redirecionar=false`), porque o container só escuta HTTP; fora dele, a chave fica no padrão, ligada.
4. **Autenticação nas APIs** (JWT ou OIDC).

---

## Referências

- **[casos-uso.md](./casos-uso.md)**: cenários da SAGA
- **[diagramas-compensacao.md](./diagramas-compensacao.md)**: estados e compensações
- **[design/escolhas-tecnicas.md](design/escolhas-tecnicas.md)**: por que Rebus, RabbitMQ, PostgreSQL e Redis (com as alternativas comparadas)
- **[Rebus](https://github.com/rebus-org/Rebus)**: documentação oficial
- **[Padrão SAGA - Microsoft](https://learn.microsoft.com/azure/architecture/reference-architectures/saga/saga)**

---

**Última atualização**: 2026-10-20 (revisão da fase 6: estado da SAGA no PostgreSQL pelo Rebus, fluxo de caixa com escrita no compose, .NET 10, observabilidade como está hoje)
