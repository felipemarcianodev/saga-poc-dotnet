# FASE 1: Fundação - Result Pattern e Estrutura Base


#### 3.1.1 Objetivos
- Criar estrutura de pastas e solution
- Implementar Result Pattern em português
- Definir contratos de mensagens

#### 3.1.2 Entregas

##### 1. **Result Pattern (SagaPoc.Shared)**
```csharp
// Tudo em português
public class Resultado<T>
public class Erro
public class Sucesso<T>
public class Falha
```

**Funcionalidades**:
- Conversão implícita
- Fluent API para encadeamento
- Suporte a múltiplos erros
- Serialização JSON
- Métodos auxiliares: `Map()`, `Bind()`, `Match()`

##### 2. **Contratos de Mensagens**
```csharp
// Comandos
public record IniciarPedido(
    Guid CorrelacaoId,
    string ClienteId,
    string RestauranteId,
    List<ItemPedido> Itens,
    string EnderecoEntrega,
    string FormaPagamento
);

public record ValidarPedidoRestaurante(
    Guid CorrelacaoId,
    string RestauranteId,
    List<ItemPedido> Itens
);

public record ProcessarPagamento(
    Guid CorrelacaoId,
    string ClienteId,
    decimal ValorTotal,
    string FormaPagamento
);

public record AlocarEntregador(
    Guid CorrelacaoId,
    string RestauranteId,
    string EnderecoEntrega,
    decimal TaxaEntrega
);

public record NotificarCliente(
    Guid CorrelacaoId,
    string ClienteId,
    string Mensagem,
    TipoNotificacao Tipo
);

// Respostas
public record PedidoRestauranteValidado(
    Guid CorrelacaoId,
    bool Valido,
    decimal ValorTotal,
    int TempoPreparoMinutos,
    string? MotivoRejeicao
);

public record PagamentoProcessado(
    Guid CorrelacaoId,
    bool Sucesso,
    string? TransacaoId,
    string? MotivoFalha
);

public record EntregadorAlocado(
    Guid CorrelacaoId,
    bool Alocado,
    string? EntregadorId,
    int TempoEstimadoMinutos,
    string? MotivoFalha
);

public record NotificacaoEnviada(
    Guid CorrelacaoId,
    bool Enviada
);

// Comandos de Compensação
public record CancelarPedidoRestaurante(Guid CorrelacaoId, string RestauranteId, Guid PedidoId);
public record EstornarPagamento(Guid CorrelacaoId, string TransacaoId);
public record LiberarEntregador(Guid CorrelacaoId, string EntregadorId);
```

##### 3. **Modelos de Domínio**
```csharp
public record ItemPedido(string ProdutoId, string Nome, int Quantidade, decimal PrecoUnitario);

public enum TipoNotificacao
{
    PedidoConfirmado,
    PedidoCancelado,
    EntregadorAlocado,
    PedidoEmPreparacao,
    PedidoSaiuParaEntrega,
    PedidoEntregue
}

public enum StatusPedido
{
    Pendente,
    Confirmado,
    EmPreparacao,
    SaiuParaEntrega,
    Entregue,
    Cancelado
}
```

##### 4. **Estrutura de Projetos**
- `SagaPoc.Shared.csproj` - Class Library
- `SagaPoc.Orquestrador.csproj` - Worker Service
- `SagaPoc.ServicoRestaurante.csproj` - Worker Service
- `SagaPoc.ServicoPagamento.csproj` - Worker Service
- `SagaPoc.ServicoEntregador.csproj` - Worker Service
- `SagaPoc.ServicoNotificacao.csproj` - Worker Service
- `SagaPoc.Api.csproj` - ASP.NET Core Web API

#### 3.1.3 Pacotes NuGet Necessários
```xml
<!-- Todos os projetos -->
<PackageReference Include="Rebus" Version="8.4.4" />
<PackageReference Include="Rebus.RabbitMq" Version="10.1.1" />
<PackageReference Include="Rebus.ServiceProvider" Version="9.0.2" />

<!-- Orquestrador (para Sagas) -->
<PackageReference Include="Rebus.Persistence.InMem" Version="3.0.0" />

<!-- Logging -->
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
```

#### 3.1.4 Critérios de Aceitação
- [ ] Result Pattern permite encadeamento fluente
- [ ] Mensagens fortemente tipadas
- [ ] Solution compila sem warnings
- [ ] Null safety habilitado em todos os projetos

---

