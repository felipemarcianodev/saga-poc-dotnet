# FASE 22: Segurança e Autenticação

## Objetivos
- Implementar autenticação segura no RabbitMQ
- Configurar SSL/TLS para comunicação entre serviços
- Adicionar criptografia de mensagens em nível de aplicação
- Proteger endpoints da API com autenticação
- Implementar secrets management para credenciais

## Entregas

### 1. **SSL/TLS no RabbitMQ**

#### Gerar Certificados

```bash
# Criar diretório para certificados
mkdir -p docker/rabbitmq/certs
cd docker/rabbitmq/certs

# Gerar CA (Certificate Authority)
openssl genrsa -out ca-key.pem 4096
openssl req -new -x509 -days 3650 -key ca-key.pem -out ca-cert.pem \
  -subj "/CN=SAGA-CA"

# Gerar certificado do servidor RabbitMQ
openssl genrsa -out server-key.pem 4096
openssl req -new -key server-key.pem -out server-req.pem \
  -subj "/CN=saga-rabbitmq"

# Assinar certificado com CA
openssl x509 -req -days 3650 -in server-req.pem \
  -CA ca-cert.pem -CAkey ca-key.pem -CAcreateserial \
  -out server-cert.pem

# Gerar certificado do cliente
openssl genrsa -out client-key.pem 4096
openssl req -new -key client-key.pem -out client-req.pem \
  -subj "/CN=saga-client"

openssl x509 -req -days 3650 -in client-req.pem \
  -CA ca-cert.pem -CAkey ca-key.pem -CAcreateserial \
  -out client-cert.pem

# Converter para PKCS12 (para .NET)
openssl pkcs12 -export -out client.pfx \
  -inkey client-key.pem -in client-cert.pem \
  -certfile ca-cert.pem -passout pass:saga123
```

#### Configurar RabbitMQ com SSL/TLS

```yaml
# docker/docker-compose.yml
rabbitmq:
  image: rabbitmq:3.13-management
  container_name: saga-rabbitmq
  hostname: saga-rabbitmq
  networks:
    - saga-network
  ports:
    - "5671:5671"   # AMQPS (SSL/TLS)
    - "5672:5672"   # AMQP (manter para desenvolvimento local)
    - "15671:15671" # Management UI HTTPS
    - "15672:15672" # Management UI HTTP (manter para dev)
  environment:
    RABBITMQ_DEFAULT_USER: saga
    RABBITMQ_DEFAULT_PASS: saga123
    RABBITMQ_DEFAULT_VHOST: /
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
    - ./rabbitmq/certs:/etc/rabbitmq/certs:ro
    - ./rabbitmq/rabbitmq.conf:/etc/rabbitmq/rabbitmq.conf:ro
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5
  restart: unless-stopped
```

#### Configuração do RabbitMQ (rabbitmq.conf)

```conf
# docker/rabbitmq/rabbitmq.conf

# SSL/TLS Configuration
listeners.ssl.default = 5671

ssl_options.cacertfile = /etc/rabbitmq/certs/ca-cert.pem
ssl_options.certfile   = /etc/rabbitmq/certs/server-cert.pem
ssl_options.keyfile    = /etc/rabbitmq/certs/server-key.pem
ssl_options.verify     = verify_peer
ssl_options.fail_if_no_peer_cert = true

# TLS versions
ssl_options.versions.1 = tlsv1.3
ssl_options.versions.2 = tlsv1.2

# Management UI HTTPS
management.ssl.port       = 15671
management.ssl.cacertfile = /etc/rabbitmq/certs/ca-cert.pem
management.ssl.certfile   = /etc/rabbitmq/certs/server-cert.pem
management.ssl.keyfile    = /etc/rabbitmq/certs/server-key.pem

# Force strong ciphers
ssl_options.honor_cipher_order = true
ssl_options.honor_ecc_order    = true
```

### 2. **Configuração SSL/TLS no Rebus (C#)**

```csharp
// src/SagaPoc.Orquestrador/Program.cs

builder.Services.AddRebus(configure => configure
    .Transport(t => t.UseRabbitMq(connectionString: BuildSecureConnectionString(), queueName: "saga-orquestrador")
        .Ssl(new SslSettings(
            enabled: true,
            serverName: "saga-rabbitmq",
            certificatePath: "/certs/client.pfx",
            certificatePassphrase: "saga123",
            acceptedPolicyErrors: SslPolicyErrors.None
        ))
    )
    .Routing(r => r.TypeBased()...)
    .Timeouts(t => t.StoreInMemory())
);

string BuildSecureConnectionString()
{
    var host = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    var username = builder.Configuration["RabbitMQ:Username"] ?? "saga";
    var password = builder.Configuration["RabbitMQ:Password"] ?? "saga123";

    // Usar porta SSL
    return $"amqps://{username}:{password}@{host}:5671";
}
```

### 3. **Criptografia de Mensagens (Application-Level)**

```csharp
// Instalar: dotnet add package System.Security.Cryptography

// src/SagaPoc.Shared/Seguranca/CriptografiaMensagens.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public interface ICriptografiaMensagens
{
    string Criptografar<T>(T mensagem);
    T Descriptografar<T>(string mensagemCriptografada);
}

public class CriptografiaMensagensAes : ICriptografiaMensagens
{
    private readonly byte[] _chave;
    private readonly byte[] _iv;

    public CriptografiaMensagensAes(string chaveBase64, string ivBase64)
    {
        _chave = Convert.FromBase64String(chaveBase64);
        _iv = Convert.FromBase64String(ivBase64);
    }

    public string Criptografar<T>(T mensagem)
    {
        var json = JsonSerializer.Serialize(mensagem);
        var dadosPlanos = Encoding.UTF8.GetBytes(json);

        using var aes = Aes.Create();
        aes.Key = _chave;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        var dadosCriptografados = encryptor.TransformFinalBlock(dadosPlanos, 0, dadosPlanos.Length);

        return Convert.ToBase64String(dadosCriptografados);
    }

    public T Descriptografar<T>(string mensagemCriptografada)
    {
        var dadosCriptografados = Convert.FromBase64String(mensagemCriptografada);

        using var aes = Aes.Create();
        aes.Key = _chave;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor();
        var dadosPlanos = decryptor.TransformFinalBlock(dadosCriptografados, 0, dadosCriptografados.Length);

        var json = Encoding.UTF8.GetString(dadosPlanos);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}
```

#### Integração com Rebus

```csharp
// src/SagaPoc.Shared/Seguranca/RebusMensagemCriptografadaPipeline.cs

public class RebusMensagemCriptografadaPipeline : IOutgoingStep, IIncomingStep
{
    private readonly ICriptografiaMensagens _criptografia;
    private readonly ILogger<RebusMensagemCriptografadaPipeline> _logger;

    public RebusMensagemCriptografadaPipeline(
        ICriptografiaMensagens criptografia,
        ILogger<RebusMensagemCriptografadaPipeline> logger)
    {
        _criptografia = criptografia;
        _logger = logger;
    }

    // Criptografar ao enviar
    public async Task Process(OutgoingStepContext context, Func<Task> next)
    {
        var mensagem = context.Load<object>();
        var mensagemCriptografada = _criptografia.Criptografar(mensagem);

        context.Save(new MensagemCriptografada(mensagemCriptografada));

        _logger.LogDebug("Mensagem criptografada: {TipoMensagem}", mensagem.GetType().Name);

        await next();
    }

    // Descriptografar ao receber
    public async Task Process(IncomingStepContext context, Func<Task> next)
    {
        var mensagemCriptografada = context.Load<MensagemCriptografada>();
        var mensagemOriginal = _criptografia.Descriptografar<object>(mensagemCriptografada.ConteudoCriptografado);

        context.Save(mensagemOriginal);

        _logger.LogDebug("Mensagem descriptografada: {TipoMensagem}", mensagemOriginal.GetType().Name);

        await next();
    }
}

public record MensagemCriptografada(string ConteudoCriptografado);
```

#### Registrar Pipeline

```csharp
// Program.cs
builder.Services.AddSingleton<ICriptografiaMensagens>(sp =>
{
    var chave = builder.Configuration["Seguranca:ChaveCriptografia"];
    var iv = builder.Configuration["Seguranca:IVCriptografia"];
    return new CriptografiaMensagensAes(chave, iv);
});

builder.Services.AddRebus(configure => configure
    .Transport(...)
    .Options(o =>
    {
        o.Decorate<IPipeline>(c =>
        {
            var pipeline = c.Get<IPipeline>();
            var step = c.Get<RebusMensagemCriptografadaPipeline>();

            return new PipelineStepInjector(pipeline)
                .OnReceive(step, PipelineRelativePosition.Before, typeof(DeserializeIncomingMessageStep))
                .OnSend(step, PipelineRelativePosition.After, typeof(SerializeOutgoingMessageStep));
        });
    })
);
```

### 4. **Secrets Management com Azure Key Vault / HashiCorp Vault**

#### Opção 1: Azure Key Vault

```bash
# Instalar pacotes
dotnet add package Azure.Identity
dotnet add package Azure.Security.KeyVault.Secrets
```

```csharp
// Program.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var keyVaultUrl = builder.Configuration["KeyVault:Url"];
var client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());

// Recuperar secrets
var rabbitMqPassword = (await client.GetSecretAsync("RabbitMQ-Password")).Value.Value;
var encryptionKey = (await client.GetSecretAsync("Encryption-Key")).Value.Value;

builder.Configuration["RabbitMQ:Password"] = rabbitMqPassword;
builder.Configuration["Seguranca:ChaveCriptografia"] = encryptionKey;
```

#### Opção 2: HashiCorp Vault (Open Source)

```yaml
# docker/docker-compose.yml
vault:
  image: hashicorp/vault:latest
  container_name: saga-vault
  environment:
    VAULT_DEV_ROOT_TOKEN_ID: saga-root-token
    VAULT_DEV_LISTEN_ADDRESS: 0.0.0.0:8200
  ports:
    - "8200:8200"
  cap_add:
    - IPC_LOCK
  networks:
    - saga-network
```

```bash
# Configurar secrets
export VAULT_ADDR='http://localhost:8200'
export VAULT_TOKEN='saga-root-token'

vault kv put secret/saga/rabbitmq \
  username=saga \
  password=SuperSecurePassword123!

vault kv put secret/saga/encryption \
  key=BASE64_ENCODED_KEY \
  iv=BASE64_ENCODED_IV
```

```csharp
// Instalar: dotnet add package VaultSharp

using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;

var vaultUri = builder.Configuration["Vault:Uri"] ?? "http://localhost:8200";
var vaultToken = builder.Configuration["Vault:Token"];

var vaultClient = new VaultClient(new VaultClientSettings(
    vaultUri,
    new TokenAuthMethodInfo(vaultToken)
));

// Ler secrets
var rabbitMqSecrets = await vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync<Dictionary<string, string>>(
    path: "saga/rabbitmq",
    mountPoint: "secret"
);

builder.Configuration["RabbitMQ:Username"] = rabbitMqSecrets.Data.Data["username"];
builder.Configuration["RabbitMQ:Password"] = rabbitMqSecrets.Data.Data["password"];
```

### 5. **Autenticação JWT na API**

```bash
# Instalar pacotes
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

```csharp
// src/SagaPoc.Api/Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar autenticação JWT
var jwtSecret = builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiUser", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Endpoints protegidos
app.MapPost("/api/pedidos", [Authorize(Policy = "ApiUser")] async (
    PedidoRequest request,
    IBus bus) =>
{
    // Criar pedido...
});

app.MapGet("/api/pedidos/{id}", [Authorize(Policy = "ApiUser")] async (
    Guid id) =>
{
    // Buscar pedido...
});

app.MapPost("/api/pedidos/{id}/cancelar", [Authorize(Policy = "Admin")] async (
    Guid id) =>
{
    // Cancelar pedido (apenas admins)...
});

app.Run();
```

### 6. **Rate Limiting e Proteção contra DDoS**

```csharp
// Instalar: dotnet add package Microsoft.AspNetCore.RateLimiting

builder.Services.AddRateLimiter(options =>
{
    // Limitar por IP
    options.AddFixedWindowLimiter("fixed", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });

    // Limitar por usuário autenticado
    options.AddPolicy("perUser", context =>
    {
        var username = context.User?.Identity?.Name ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(username, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1000,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

app.UseRateLimiter();

app.MapPost("/api/pedidos", async (PedidoRequest request, IBus bus) =>
{
    // Criar pedido...
}).RequireRateLimiting("perUser");
```

### 7. **Configuração de Produção (appsettings.Production.json)**

```json
{
  "RabbitMQ": {
    "Host": "rabbitmq.producao.empresa.com",
    "Port": 5671,
    "Username": "saga-prod",
    "Password": "#{RabbitMQ:Password}#",
    "UseSsl": true,
    "CertificatePath": "/app/certs/client.pfx",
    "CertificatePassword": "#{Certificate:Password}#"
  },
  "Seguranca": {
    "ChaveCriptografia": "#{Encryption:Key}#",
    "IVCriptografia": "#{Encryption:IV}#"
  },
  "Jwt": {
    "Secret": "#{Jwt:Secret}#",
    "Issuer": "https://saga-api.producao.empresa.com",
    "Audience": "saga-clients",
    "ExpirationMinutes": 60
  },
  "KeyVault": {
    "Url": "https://saga-keyvault.vault.azure.net/"
  },
  "Vault": {
    "Uri": "https://vault.producao.empresa.com:8200",
    "Token": "#{Vault:Token}#"
  }
}
```

## Critérios de Aceitação

- [ ] SSL/TLS configurado no RabbitMQ com certificados válidos
- [ ] Todos os serviços conectam ao RabbitMQ via AMQPS (porta 5671)
- [ ] Mensagens sensíveis são criptografadas em nível de aplicação
- [ ] Secrets (senhas, chaves) são armazenados em Vault/Key Vault
- [ ] API requer autenticação JWT para todos os endpoints
- [ ] Rate limiting configurado e testado
- [ ] Documentação de rotação de certificados criada
- [ ] Nenhuma credencial hardcoded no código ou configurações
- [ ] Logs não expõem informações sensíveis

## Trade-offs

**Benefícios:**
- ✅ Comunicação criptografada end-to-end
- ✅ Proteção contra interceptação de mensagens
- ✅ Secrets gerenciados centralmente
- ✅ Conformidade com requisitos de segurança (LGPD, PCI-DSS)

**Considerações:**
- ⚠️ Overhead de performance (5-10% por criptografia)
- ⚠️ Complexidade adicional na infraestrutura
- ⚠️ Necessidade de gerenciar certificados e rotação

## Estimativa

**Tempo Total**: 6-10 horas

- SSL/TLS RabbitMQ: 2-3 horas
- Criptografia de mensagens: 2-3 horas
- Secrets management: 1-2 horas
- Autenticação JWT: 1-2 horas
- Testes e validação: 1-2 horas

---

**Versão**: 1.0
**Data de criação**: 2026-01-08
**Última atualização**: 2026-01-08
