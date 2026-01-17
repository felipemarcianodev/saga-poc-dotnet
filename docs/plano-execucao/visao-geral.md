# Visão Geral do Projeto


### 1.1 Objetivo
Criar uma Proof of Concept (POC) demonstrando a implementação do **padrão SAGA Orquestrado** utilizando **Rebus** e **RabbitMQ** para comunicação entre microsserviços, aplicando o **Result Pattern** para tratamento de resultados.

### 1.2 Escopo
- **Domínio**: Sistema de Delivery de Comida
- **Padrões**: SAGA Orquestrado + Result Pattern
- **Arquitetura**: Microsserviços com mensageria
- **Mensageria**: Rebus + RabbitMQ (Open Source)
- **Linguagem**: C# (.NET 9.0)
- **Idioma**: Português (código, documentação, tudo)
- **Casos de Uso**: Mínimo 10 cenários com compensações

# Estrutura Final de Pastas


```
saga-poc-dotnet/
│
├── docs/
│   ├── plano-execucao.md
│   ├── ARQUITETURA.md
│   ├── REBUS-GUIDE.md
│   └── CASOS-DE-USO.md
│
├── src/
│   ├── BuildingBlocks/
│   │   ├── SagaPoc.Common/
│   │   │   ├── ResultPattern/
│   │   │   │   ├── Resultado.cs
│   │   │   │   ├── Erro.cs
│   │   │   │   └── ResultadoExtensions.cs
│   │   │   ├── Mensagens/
│   │   │   │   ├── Comandos/
│   │   │   │   ├── Eventos/
│   │   │   │   └── Respostas/
│   │   │   └── Modelos/
│   │   ├── SagaPoc.Observability/
│   │   ├── WebHost/
│   │   ├── SagaPoc.Infrastructure/
│   │   └── SagaPoc.Infrastructure.Core/
│   │
│   ├── SagaPoc.Orquestrador/
│   │   ├── Sagas/
│   │   │   ├── PedidoSaga.cs
│   │   │   └── EstadoPedido.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── SagaPoc.ServicoRestaurante/
│   │   ├── Consumers/
│   │   ├── Servicos/
│   │   └── Program.cs
│   │
│   ├── SagaPoc.ServicoPagamento/
│   │   ├── Consumers/
│   │   └── Servicos/
│   │
│   ├── SagaPoc.ServicoEntregador/
│   │   ├── Consumers/
│   │   └── Servicos/
│   │
│   ├── SagaPoc.ServicoNotificacao/
│   │   ├── Consumers/
│   │   └── Servicos/
│   │
│   └── SagaPoc.Api/
│       ├── Controllers/
│       └── Program.cs
│
├── .gitignore
├── LICENSE
├── README.md
└── SagaPoc.sln
```

---


# Referências

- [Rebus Documentation](https://github.com/rebus-org/Rebus/)
- [Rebus Sagas](https://github.com/rebus-org/Rebus/documentation/patterns/saga)
- [RabbitMQ](https://docs.microsoft.com/azure/service-bus-messaging/)
- [Result Pattern - Vladimir Khorikov](https://enterprisecraftsmanship.com/posts/functional-c-handling-failures-input-errors/)
- [SAGA Pattern - Microsoft](https://docs.microsoft.com/azure/architecture/reference-architectures/saga/saga)
- [Outbox Pattern](https://microservices.io/patterns/data/transactional-outbox.html)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

