# Swagger/OpenAPI - API de Fluxo de Caixa

Este documento descreve a configuração e uso do Swagger/OpenAPI para a API de Fluxo de Caixa.

---

## Visão Geral

A API de Fluxo de Caixa utiliza Swagger/OpenAPI para documentação interativa dos endpoints, permitindo:

- Visualização de todos os endpoints disponíveis
- Testes interativos diretamente no navegador
- Geração automática de clientes (client SDKs)
- Documentação sempre atualizada e sincronizada com o código

---

## Acessando o Swagger UI

### Desenvolvimento Local

```
http://localhost:5000/swagger
```

ou

```
https://localhost:5001/swagger
```

### Docker

```
http://localhost:5100/swagger
```

---

## Configuração

A configuração do Swagger está localizada em `Program.cs` da API:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "API de Fluxo de Caixa",
        Version = "v1",
        Description = @"
            API para controle de fluxo de caixa com lançamentos e consolidado diário.

            **Arquitetura:** CQRS + Event-Driven

            **NFRs Atendidos:**
            - 50 requisições/segundo no consolidado
            - Disponibilidade independente entre serviços
            - < 5% de perda de requisições
            - Latência P95 < 10ms (com cache)

            **Endpoints Principais:**
            - POST /api/lancamentos - Registrar lançamento
            - GET /api/consolidado/{comerciante}/{data} - Consultar consolidado
            ",
        Contact = new()
        {
            Name = "Equipe Backend",
            Email = "backend@empresa.com"
        }
    });

    // Comentários XML
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Servidores
    options.AddServer(new()
    {
        Url = "http://localhost:5000",
        Description = "Desenvolvimento Local"
    });

    options.AddServer(new()
    {
        Url = "https://api-fluxocaixa.empresa.com",
        Description = "Produção"
    });
});
```

---

## Documentando Endpoints

### Usando Atributos

```csharp
/// <summary>
/// Registrar um novo lançamento (débito ou crédito)
/// </summary>
/// <param name="request">Dados do lançamento</param>
/// <returns>Confirmação do registro</returns>
/// <response code="202">Lançamento aceito e enviado para processamento</response>
/// <response code="400">Dados inválidos</response>
/// <response code="500">Erro interno do servidor</response>
[HttpPost]
[ProducesResponseType(typeof(LancamentoRegistradoResponse), StatusCodes.Status202Accepted)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> RegistrarLancamento(
    [FromBody] RegistrarLancamentoRequest request)
{
    // Implementação
}
```

### Usando Swagger Annotations

Para documentação mais rica, instale o pacote:

```bash
dotnet add package Swashbuckle.AspNetCore.Annotations
```

E use atributos como:

```csharp
[SwaggerOperation(
    Summary = "Consultar consolidado diário",
    Description = "Retorna o consolidado de um comerciante para uma data específica",
    OperationId = "ObterConsolidado",
    Tags = new[] { "Consolidado" }
)]
[SwaggerResponse(200, "Consolidado encontrado", typeof(ConsolidadoDiarioResponse))]
[SwaggerResponse(404, "Consolidado não encontrado")]
[SwaggerResponse(429, "Rate limit excedido (50 req/s)")]
```

---

## Endpoints Documentados

### 1. Registrar Lançamento

**POST** `/api/lancamentos`

Registra um novo lançamento de débito ou crédito.

**Request Body**:
```json
{
  "tipo": 2,
  "valor": 150.00,
  "dataLancamento": "2026-01-15",
  "descricao": "Venda de produto X",
  "comerciante": "COM001",
  "categoria": "Vendas"
}
```

**Response** (202 Accepted):
```json
{
  "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "mensagem": "Lançamento enviado para processamento"
}
```

---

### 2. Consultar Consolidado Diário

**GET** `/api/consolidado/{comerciante}/{data}`

Retorna o consolidado de um comerciante para uma data específica.

**Parâmetros**:
- `comerciante` (path): ID do comerciante (ex: "COM001")
- `data` (path): Data no formato yyyy-MM-dd (ex: "2026-01-15")

**Response** (200 OK):
```json
{
  "data": "2026-01-15",
  "comerciante": "COM001",
  "totalCreditos": 500.00,
  "totalDebitos": 150.00,
  "saldoDiario": 350.00,
  "quantidadeCreditos": 1,
  "quantidadeDebitos": 1,
  "quantidadeTotalLancamentos": 2,
  "ultimaAtualizacao": "2026-01-15T14:30:00Z"
}
```

**Headers de Cache**:
```
X-Cache-Status: HIT-L1
X-Cache-Age: 45s
Cache-Control: public, max-age=60
```

---

### 3. Consultar Consolidado por Período

**GET** `/api/consolidado/{comerciante}/periodo?inicio={data}&fim={data}`

Retorna consolidados de um comerciante para um período.

**Parâmetros**:
- `comerciante` (path): ID do comerciante
- `inicio` (query): Data inicial (yyyy-MM-dd)
- `fim` (query): Data final (yyyy-MM-dd)

**Response** (200 OK):
```json
[
  {
    "data": "2026-01-15",
    "comerciante": "COM001",
    "totalCreditos": 500.00,
    "totalDebitos": 150.00,
    "saldoDiario": 350.00
  },
  {
    "data": "2026-01-16",
    "comerciante": "COM001",
    "totalCreditos": 300.00,
    "totalDebitos": 100.00,
    "saldoDiario": 200.00
  }
]
```

---

## Schemas Documentados

### RegistrarLancamentoRequest

```json
{
  "tipo": 1 | 2,  // 1 = Débito, 2 = Crédito
  "valor": "decimal (> 0)",
  "dataLancamento": "yyyy-MM-dd",
  "descricao": "string (max 500 chars)",
  "comerciante": "string (max 100 chars)",
  "categoria": "string (opcional, max 100 chars)"
}
```

**Validações**:
- `tipo`: Obrigatório, 1 (Débito) ou 2 (Crédito)
- `valor`: Obrigatório, maior que zero
- `dataLancamento`: Obrigatório, formato yyyy-MM-dd
- `descricao`: Obrigatório, máximo 500 caracteres
- `comerciante`: Obrigatório, máximo 100 caracteres
- `categoria`: Opcional, máximo 100 caracteres

---

### ConsolidadoDiarioResponse

```json
{
  "data": "yyyy-MM-dd",
  "comerciante": "string",
  "totalCreditos": "decimal",
  "totalDebitos": "decimal",
  "saldoDiario": "decimal (computed)",
  "quantidadeCreditos": "integer",
  "quantidadeDebitos": "integer",
  "quantidadeTotalLancamentos": "integer (computed)",
  "ultimaAtualizacao": "datetime (ISO 8601)"
}
```

---

## Testando via Swagger UI

### Passo 1: Acessar o Swagger UI

Abra o navegador em `http://localhost:5000/swagger`

### Passo 2: Expandir o Endpoint

Clique no endpoint que deseja testar (ex: POST /api/lancamentos)

### Passo 3: Clicar em "Try it out"

Isso habilita o formulário de teste interativo

### Passo 4: Preencher os Dados

Edite o JSON de exemplo com seus dados:

```json
{
  "tipo": 2,
  "valor": 150.00,
  "dataLancamento": "2026-01-15",
  "descricao": "Venda de produto X",
  "comerciante": "COM001",
  "categoria": "Vendas"
}
```

### Passo 5: Executar

Clique em "Execute" para enviar a requisição

### Passo 6: Ver Resposta

O Swagger exibirá:
- Status code (202, 400, 500, etc.)
- Headers de resposta
- Body da resposta
- Curl command equivalente

---

## Exportar Especificação OpenAPI

### JSON

```
http://localhost:5000/swagger/v1/swagger.json
```

### YAML

Para obter em YAML, use ferramentas como:

```bash
curl http://localhost:5000/swagger/v1/swagger.json | \
  yq eval -P - > openapi.yaml
```

---

## Gerar Cliente SDK

### C# (NSwag)

```bash
# Instalar NSwag CLI
dotnet tool install -g NSwag.ConsoleCore

# Gerar cliente
nswag openapi2csclient \
  /input:http://localhost:5000/swagger/v1/swagger.json \
  /output:FluxoCaixaClient.cs \
  /namespace:FluxoCaixa.Client
```

### TypeScript (openapi-generator)

```bash
npx @openapitools/openapi-generator-cli generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g typescript-axios \
  -o ./src/generated
```

### Python (openapi-generator)

```bash
openapi-generator generate \
  -i http://localhost:5000/swagger/v1/swagger.json \
  -g python \
  -o ./fluxocaixa-client
```

---

## Habilitar Comentários XML

### 1. Editar o .csproj

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

### 2. Adicionar Comentários no Código

```csharp
/// <summary>
/// Registrar um novo lançamento
/// </summary>
/// <param name="request">Dados do lançamento</param>
/// <returns>Confirmação do registro</returns>
[HttpPost]
public async Task<IActionResult> RegistrarLancamento(
    [FromBody] RegistrarLancamentoRequest request)
{
    // ...
}
```

### 3. Os comentários aparecerão automaticamente no Swagger UI

---

## Segurança (Produção)

### Adicionar Autenticação no Swagger

```csharp
options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
    Description = "JWT Authorization header using Bearer scheme",
    Name = "Authorization",
    In = ParameterLocation.Header,
    Type = SecuritySchemeType.ApiKey,
    Scheme = "Bearer"
});

options.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
});
```

### Desabilitar Swagger em Produção (Opcional)

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

---

## Referências

- [Swashbuckle Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [OpenAPI Specification](https://spec.openapis.org/oas/latest.html)
- [Swagger UI](https://swagger.io/tools/swagger-ui/)

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
