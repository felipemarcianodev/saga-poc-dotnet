# FASE 2: Configuração Rebus + RabbitMQ


#### 3.2.1 Objetivos
- Configurar Rebus em todos os serviços
- Configurar RabbitMQ (host, username, password em appsettings)
- Implementar health checks

#### 3.2.2 Entregas

##### 1. **Configuração Base (Cada Serviço)**
```csharp
// Program.cs ou Startup.cs
services.AddRebus(configure => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMq(
        $"amqp://{configuration["RabbitMQ:Username"]}:{configuration["RabbitMQ:Password"]}@{configuration["RabbitMQ:Host"]}",
        "fila-restaurante"))
    .Routing(r => r.TypeBased()
        .MapAssemblyOf<ValidarPedidoRestaurante>("fila-restaurante"))
);

// Registrar handlers automaticamente
services.AutoRegisterHandlersFromAssemblyOf<ValidarPedidoRestauranteHandler>();
```

##### 2. **Configuração da API**
```csharp
// Program.cs - API apenas publica mensagens, não consome
services.AddRebus(configure => configure
    .Logging(l => l.Serilog())
    .Transport(t => t.UseRabbitMqAsOneWayClient(
        $"amqp://{configuration["RabbitMQ:Username"]}:{configuration["RabbitMQ:Password"]}@{configuration["RabbitMQ:Host"]}"))
    .Routing(r => r.TypeBased()
        .Map<IniciarPedido>("fila-orquestrador"))
);
```

##### 3. **appsettings.json**
```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Username": "saga",
    "Password": "saga123"
  },
  "Serilog": {
    "MinimumLevel": "Information"
  }
}
```

##### 4. **Docker Compose para RabbitMQ**
```yaml
version: '3.8'

services:
  rabbitmq:
    image: rabbitmq:3.13-management
    container_name: saga-rabbitmq
    ports:
      - "5672:5672"   # AMQP
      - "15672:15672" # Management UI
    environment:
      RABBITMQ_DEFAULT_USER: saga
      RABBITMQ_DEFAULT_PASS: saga123
```

#### 3.2.3 Critérios de Aceitação
- [ ] Todos os serviços conectam ao RabbitMQ
- [ ] Filas criadas automaticamente
- [ ] Health checks retornam status OK
- [ ] Logs estruturados com Serilog
- [ ] RabbitMQ Management UI acessível (http://localhost:15672)

---

