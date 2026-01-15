# FASE 24: Implementação do Serviço de Lançamentos (Write Model)

## Objetivos

- Implementar o serviço transacional de controle de lançamentos
- Aplicar TDD para desenvolvimento das regras de negócio
- Garantir princípios SOLID e Clean Code
- Implementar padrões Repository e Unit of Work
- Configurar persistência com Entity Framework Core e PostgreSQL
- Garantir alta disponibilidade conforme NFR

## Entregas

### 1. **Domínio - Agregados e Entidades**

#### Agregado Raiz: Lancamento

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/Agregados/Lancamento.cs

using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Domain.Agregados;

public class Lancamento : AggregateRoot
{
    public Guid Id { get; private set; }
    public EnumTipoLancamento Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public DateTime DataLancamento { get; private set; }
    public string Descricao { get; private set; }
    public string Comerciante { get; private set; }
    public string? Categoria { get; private set; }
    public EnumStatusLancamento Status { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime? AtualizadoEm { get; private set; }

    // Para EF Core
    private Lancamento() { }

    public static Result<Lancamento> Criar(
        EnumTipoLancamento tipo,
        decimal valor,
        DateTime dataLancamento,
        string descricao,
        string comerciante,
        string? categoria = null)
    {
        // Validações de domínio
        var validacao = ValidarParametros(tipo, valor, descricao, comerciante);
        if (validacao.IsFailure)
            return Result<Lancamento>.Failure(validacao.Error);

        var lancamento = new Lancamento
        {
            Id = Guid.NewGuid(),
            Tipo = tipo,
            Valor = valor,
            DataLancamento = dataLancamento.Date,
            Descricao = descricao.Trim(),
            Comerciante = comerciante.Trim(),
            Categoria = categoria?.Trim(),
            Status = EnumStatusLancamento.Pendente,
            CriadoEm = DateTime.UtcNow
        };

        // Adicionar evento de domínio
        var evento = tipo == EnumTipoLancamento.Credito
            ? new LancamentoCreditoRegistrado(
                lancamento.Id,
                lancamento.Valor,
                lancamento.DataLancamento,
                lancamento.Descricao,
                lancamento.Comerciante,
                lancamento.Categoria)
            : new LancamentoDebitoRegistrado(
                lancamento.Id,
                lancamento.Valor,
                lancamento.DataLancamento,
                lancamento.Descricao,
                lancamento.Comerciante,
                lancamento.Categoria);

        lancamento.AdicionarEvento(evento);

        return Result<Lancamento>.Success(lancamento);
    }

    public Result Confirmar()
    {
        if (Status == EnumStatusLancamento.Confirmado)
            return Result.Failure(new Erro(
                "Lancamento.JaConfirmado",
                "Lançamento já foi confirmado anteriormente"));

        if (Status == EnumStatusLancamento.Cancelado)
            return Result.Failure(new Erro(
                "Lancamento.Cancelado",
                "Não é possível confirmar um lançamento cancelado"));

        Status = EnumStatusLancamento.Confirmado;
        AtualizadoEm = DateTime.UtcNow;

        return Result.Success();
    }

    public Result Cancelar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Failure(new Erro(
                "Lancamento.MotivoObrigatorio",
                "O motivo do cancelamento é obrigatório"));

        if (Status == EnumStatusLancamento.Cancelado)
            return Result.Failure(new Erro(
                "Lancamento.JaCancelado",
                "Lançamento já foi cancelado anteriormente"));

        Status = EnumStatusLancamento.Cancelado;
        AtualizadoEm = DateTime.UtcNow;

        AdicionarEvento(new LancamentoCancelado(Id, motivo, DateTime.UtcNow));

        return Result.Success();
    }

    private static Result ValidarParametros(
        EnumTipoLancamento tipo,
        decimal valor,
        string descricao,
        string comerciante)
    {
        if (valor <= 0)
            return Result.Failure(new Erro(
                "Lancamento.ValorInvalido",
                "O valor do lançamento deve ser maior que zero"));

        if (valor > 1_000_000_000) // 1 bilhão
            return Result.Failure(new Erro(
                "Lancamento.ValorExcessivo",
                "O valor do lançamento excede o limite permitido"));

        if (string.IsNullOrWhiteSpace(descricao))
            return Result.Failure(new Erro(
                "Lancamento.DescricaoObrigatoria",
                "A descrição do lançamento é obrigatória"));

        if (descricao.Length > 500)
            return Result.Failure(new Erro(
                "Lancamento.DescricaoMuitoLonga",
                "A descrição não pode exceder 500 caracteres"));

        if (string.IsNullOrWhiteSpace(comerciante))
            return Result.Failure(new Erro(
                "Lancamento.ComercianteObrigatorio",
                "O identificador do comerciante é obrigatório"));

        return Result.Success();
    }
}
```

#### Value Objects

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/ValueObjects/EnumTipoLancamento.cs

namespace SagaPoc.FluxoCaixa.Domain.ValueObjects;

public enum EnumTipoLancamento
{
    Debito = 1,
    Credito = 2
}

public enum EnumStatusLancamento
{
    Pendente = 1,
    Confirmado = 2,
    Cancelado = 3
}
```

#### Eventos de Domínio

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/Eventos/LancamentoCreditoRegistrado.cs

namespace SagaPoc.FluxoCaixa.Domain.Eventos;

public record LancamentoCreditoRegistrado(
    Guid LancamentoId,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante,
    string? Categoria
);

public record LancamentoDebitoRegistrado(
    Guid LancamentoId,
    decimal Valor,
    DateTime DataLancamento,
    string Descricao,
    string Comerciante,
    string? Categoria
);

public record LancamentoCancelado(
    Guid LancamentoId,
    string Motivo,
    DateTime DataCancelamento
);
```

### 2. **Repositórios (Repository Pattern)**

#### Interface do Repositório

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/Repositorios/ILancamentoRepository.cs

using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Domain.Repositorios;

public interface ILancamentoRepository
{
    Task<Result<Lancamento>> AdicionarAsync(Lancamento lancamento, CancellationToken ct = default);
    Task<Result<Lancamento>> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default);
    Task<Result<IEnumerable<Lancamento>>> ObterPorDataAsync(
        string comerciante,
        DateTime data,
        CancellationToken ct = default);
    Task<Result> AtualizarAsync(Lancamento lancamento, CancellationToken ct = default);
}
```

#### Implementação com EF Core

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/Repositorios/LancamentoRepository.cs

using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using SagaPoc.Shared.ResultPattern;

namespace SagaPoc.FluxoCaixa.Infrastructure.Repositorios;

public class LancamentoRepository : ILancamentoRepository
{
    private readonly FluxoCaixaDbContext _context;
    private readonly ILogger<LancamentoRepository> _logger;

    public LancamentoRepository(
        FluxoCaixaDbContext context,
        ILogger<LancamentoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Lancamento>> AdicionarAsync(
        Lancamento lancamento,
        CancellationToken ct = default)
    {
        try
        {
            await _context.Lancamentos.AddAsync(lancamento, ct);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Lançamento {LancamentoId} adicionado com sucesso. Tipo: {Tipo}, Valor: {Valor}",
                lancamento.Id,
                lancamento.Tipo,
                lancamento.Valor);

            return Result<Lancamento>.Success(lancamento);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro ao adicionar lançamento {LancamentoId}", lancamento.Id);
            return Result<Lancamento>.Failure(new Erro(
                "Lancamento.ErroPersistencia",
                "Erro ao persistir o lançamento no banco de dados"));
        }
    }

    public async Task<Result<Lancamento>> ObterPorIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var lancamento = await _context.Lancamentos
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id, ct);

            if (lancamento is null)
            {
                return Result<Lancamento>.Failure(new Erro(
                    "Lancamento.NaoEncontrado",
                    $"Lançamento {id} não foi encontrado"));
            }

            return Result<Lancamento>.Success(lancamento);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar lançamento {LancamentoId}", id);
            return Result<Lancamento>.Failure(new Erro(
                "Lancamento.ErroBusca",
                "Erro ao buscar o lançamento"));
        }
    }

    public async Task<Result<IEnumerable<Lancamento>>> ObterPorPeriodoAsync(
        string comerciante,
        DateTime inicio,
        DateTime fim,
        CancellationToken ct = default)
    {
        try
        {
            var lancamentos = await _context.Lancamentos
                .AsNoTracking()
                .Where(l => l.Comerciante == comerciante
                         && l.DataLancamento >= inicio.Date
                         && l.DataLancamento <= fim.Date)
                .OrderBy(l => l.DataLancamento)
                .ToListAsync(ct);

            return Result<IEnumerable<Lancamento>>.Success(lancamentos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Erro ao buscar lançamentos por período. Comerciante: {Comerciante}, Período: {Inicio} - {Fim}",
                comerciante, inicio, fim);

            return Result<IEnumerable<Lancamento>>.Failure(new Erro(
                "Lancamento.ErroBusca",
                "Erro ao buscar lançamentos por período"));
        }
    }

    public async Task<Result<IEnumerable<Lancamento>>> ObterPorDataAsync(
        string comerciante,
        DateTime data,
        CancellationToken ct = default)
    {
        return await ObterPorPeriodoAsync(comerciante, data.Date, data.Date, ct);
    }

    public async Task<Result> AtualizarAsync(
        Lancamento lancamento,
        CancellationToken ct = default)
    {
        try
        {
            _context.Lancamentos.Update(lancamento);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Lançamento {LancamentoId} atualizado com sucesso", lancamento.Id);

            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Erro ao atualizar lançamento {LancamentoId}", lancamento.Id);
            return Result.Failure(new Erro(
                "Lancamento.ErroAtualizacao",
                "Erro ao atualizar o lançamento"));
        }
    }
}
```

### 3. **DbContext e Configurações EF Core**

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/Persistencia/FluxoCaixaDbContext.cs

using Microsoft.EntityFrameworkCore;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Infrastructure.Persistencia;

public class FluxoCaixaDbContext : DbContext
{
    public DbSet<Lancamento> Lancamentos { get; set; }

    public FluxoCaixaDbContext(DbContextOptions<FluxoCaixaDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FluxoCaixaDbContext).Assembly);
    }
}
```

#### Configuração da Entidade Lancamento

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/Persistencia/Configuracoes/LancamentoConfiguration.cs

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SagaPoc.FluxoCaixa.Domain.Agregados;

namespace SagaPoc.FluxoCaixa.Infrastructure.Persistencia.Configuracoes;

public class LancamentoConfiguration : IEntityTypeConfiguration<Lancamento>
{
    public void Configure(EntityTypeBuilder<Lancamento> builder)
    {
        builder.ToTable("lancamentos");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(l => l.Tipo)
            .HasColumnName("tipo")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.Valor)
            .HasColumnName("valor")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(l => l.DataLancamento)
            .HasColumnName("data_lancamento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(l => l.Comerciante)
            .HasColumnName("comerciante")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.Categoria)
            .HasColumnName("categoria")
            .HasMaxLength(100);

        builder.Property(l => l.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();

        builder.Property(l => l.AtualizadoEm)
            .HasColumnName("atualizado_em");

        // Índices para otimização de consultas
        builder.HasIndex(l => new { l.Comerciante, l.DataLancamento })
            .HasDatabaseName("idx_lancamentos_comerciante_data");

        builder.HasIndex(l => l.DataLancamento)
            .HasDatabaseName("idx_lancamentos_data");

        builder.HasIndex(l => l.Status)
            .HasDatabaseName("idx_lancamentos_status");

        // Ignorar propriedade de eventos de domínio (não persistir)
        builder.Ignore(l => l.EventosDominio);
    }
}
```

### 4. **Handlers de Comandos**

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Lancamentos/Handlers/RegistrarLancamentoHandler.cs

using Rebus.Handlers;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;
using Rebus.Bus;

namespace SagaPoc.FluxoCaixa.Lancamentos.Handlers;

public class RegistrarLancamentoHandler : IHandleMessages<RegistrarLancamento>
{
    private readonly ILancamentoRepository _repository;
    private readonly IBus _bus;
    private readonly ILogger<RegistrarLancamentoHandler> _logger;

    public RegistrarLancamentoHandler(
        ILancamentoRepository repository,
        IBus bus,
        ILogger<RegistrarLancamentoHandler> logger)
    {
        _repository = repository;
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(RegistrarLancamento comando)
    {
        _logger.LogInformation(
            "Processando comando RegistrarLancamento. Tipo: {Tipo}, Valor: {Valor}, Comerciante: {Comerciante}",
            comando.Tipo,
            comando.Valor,
            comando.Comerciante);

        // Criar agregado usando factory method
        var resultadoCriacao = Lancamento.Criar(
            comando.Tipo,
            comando.Valor,
            comando.DataLancamento,
            comando.Descricao,
            comando.Comerciante,
            comando.Categoria);

        if (resultadoCriacao.IsFailure)
        {
            _logger.LogWarning(
                "Falha ao criar lançamento: {Erro}",
                resultadoCriacao.Error.Mensagem);

            await _bus.Reply(new LancamentoRejeitado(
                comando.CorrelationId,
                resultadoCriacao.Error.Codigo,
                resultadoCriacao.Error.Mensagem));

            return;
        }

        var lancamento = resultadoCriacao.Value;

        // Persistir
        var resultadoPersistencia = await _repository.AdicionarAsync(lancamento);

        if (resultadoPersistencia.IsFailure)
        {
            _logger.LogError(
                "Falha ao persistir lançamento: {Erro}",
                resultadoPersistencia.Error.Mensagem);

            await _bus.Reply(new LancamentoRejeitado(
                comando.CorrelationId,
                resultadoPersistencia.Error.Codigo,
                resultadoPersistencia.Error.Mensagem));

            return;
        }

        // Confirmar lançamento
        lancamento.Confirmar();
        await _repository.AtualizarAsync(lancamento);

        // Publicar eventos de domínio
        foreach (var evento in lancamento.EventosDominio)
        {
            await _bus.Publish(evento);
        }

        // Responder com sucesso
        await _bus.Reply(new LancamentoRegistradoComSucesso(
            comando.CorrelationId,
            lancamento.Id,
            lancamento.Tipo,
            lancamento.Valor,
            lancamento.DataLancamento));

        _logger.LogInformation(
            "Lançamento {LancamentoId} registrado e confirmado com sucesso",
            lancamento.Id);
    }
}
```

### 5. **API REST - Endpoints**

```csharp
// src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api/Controllers/LancamentosController.cs

using Microsoft.AspNetCore.Mvc;
using Rebus.Bus;
using SagaPoc.FluxoCaixa.Api.DTOs;
using SagaPoc.FluxoCaixa.Domain.Comandos;
using SagaPoc.FluxoCaixa.Domain.Repositorios;

namespace SagaPoc.FluxoCaixa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LancamentosController : ControllerBase
{
    private readonly IBus _bus;
    private readonly ILancamentoRepository _repository;
    private readonly ILogger<LancamentosController> _logger;

    public LancamentosController(
        IBus bus,
        ILancamentoRepository repository,
        ILogger<LancamentosController> logger)
    {
        _bus = bus;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Registra um novo lançamento (débito ou crédito)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarLancamentoRequest request,
        CancellationToken ct)
    {
        var comando = new RegistrarLancamento(
            Guid.NewGuid(), // CorrelationId
            request.Tipo,
            request.Valor,
            request.DataLancamento ?? DateTime.Today,
            request.Descricao,
            request.Comerciante,
            request.Categoria);

        await _bus.Send(comando);

        _logger.LogInformation(
            "Comando RegistrarLancamento enviado. CorrelationId: {CorrelationId}",
            comando.CorrelationId);

        return Accepted(new
        {
            correlationId = comando.CorrelationId,
            mensagem = "Lançamento enviado para processamento"
        });
    }

    /// <summary>
    /// Consulta um lançamento por ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id, CancellationToken ct)
    {
        var resultado = await _repository.ObterPorIdAsync(id, ct);

        if (resultado.IsFailure)
            return NotFound(new { erro = resultado.Error.Mensagem });

        var lancamento = resultado.Value;

        return Ok(new LancamentoResponse
        {
            Id = lancamento.Id,
            Tipo = lancamento.Tipo.ToString(),
            Valor = lancamento.Valor,
            DataLancamento = lancamento.DataLancamento,
            Descricao = lancamento.Descricao,
            Comerciante = lancamento.Comerciante,
            Categoria = lancamento.Categoria,
            Status = lancamento.Status.ToString(),
            CriadoEm = lancamento.CriadoEm
        });
    }

    /// <summary>
    /// Lista lançamentos por período
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(
        [FromQuery] string comerciante,
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        CancellationToken ct)
    {
        var dataInicio = inicio ?? DateTime.Today.AddDays(-30);
        var dataFim = fim ?? DateTime.Today;

        var resultado = await _repository.ObterPorPeriodoAsync(
            comerciante,
            dataInicio,
            dataFim,
            ct);

        if (resultado.IsFailure)
            return BadRequest(new { erro = resultado.Error.Mensagem });

        var lancamentos = resultado.Value.Select(l => new LancamentoResponse
        {
            Id = l.Id,
            Tipo = l.Tipo.ToString(),
            Valor = l.Valor,
            DataLancamento = l.DataLancamento,
            Descricao = l.Descricao,
            Comerciante = l.Comerciante,
            Categoria = l.Categoria,
            Status = l.Status.ToString(),
            CriadoEm = l.CriadoEm
        });

        return Ok(lancamentos);
    }
}
```

### 6. **Migration EF Core**

```bash
# Criar projeto de Infrastructure
dotnet new classlib -n SagaPoc.FluxoCaixa.Infrastructure -f net9.0

# Adicionar pacotes
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# Criar migration
dotnet ef migrations add CriarTabelaLancamentos \
  --project src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure \
  --startup-project src/SagaPoc.FluxoCaixa.Api \
  --context FluxoCaixaDbContext

# Aplicar migration
dotnet ef database update \
  --project src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure \
  --startup-project src/SagaPoc.FluxoCaixa.Api
```

### 7. **Configuração (appsettings.json)**

```json
{
  "ConnectionStrings": {
    "FluxoCaixaDb": "Host=localhost;Port=5432;Database=fluxocaixa_lancamentos;Username=saga;Password=saga123"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "saga",
    "Password": "saga123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### 8. **Docker Compose - PostgreSQL**

```yaml
# docker/docker-compose.yml (adicionar ao existente)

services:
  postgres-fluxocaixa:
    image: postgres:16
    container_name: fluxocaixa-postgres
    environment:
      POSTGRES_DB: fluxocaixa_lancamentos
      POSTGRES_USER: saga
      POSTGRES_PASSWORD: saga123
    ports:
      - "5433:5432"
    volumes:
      - postgres_fluxocaixa_data:/var/lib/postgresql/data
    networks:
      - saga-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U saga"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_fluxocaixa_data:
```

## Testes Unitários (TDD)

### Teste do Agregado Lancamento

```csharp
// tests/SagaPoc.FluxoCaixa.Domain.Tests/Agregados/LancamentoTests.cs

using FluentAssertions;
using SagaPoc.FluxoCaixa.Domain.Agregados;
using SagaPoc.FluxoCaixa.Domain.ValueObjects;
using Xunit;

namespace SagaPoc.FluxoCaixa.Domain.Tests.Agregados;

public class LancamentoTests
{
    [Fact]
    public void Criar_ComParametrosValidos_DeveRetornarSucesso()
    {
        // Arrange
        var tipo = EnumTipoLancamento.Credito;
        var valor = 100.50m;
        var data = DateTime.Today;
        var descricao = "Venda de produto X";
        var comerciante = "COM001";

        // Act
        var resultado = Lancamento.Criar(tipo, valor, data, descricao, comerciante);

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Tipo.Should().Be(tipo);
        resultado.Value.Valor.Should().Be(valor);
        resultado.Value.Descricao.Should().Be(descricao);
        resultado.Value.Status.Should().Be(EnumStatusLancamento.Pendente);
        resultado.Value.EventosDominio.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Criar_ComValorInvalido_DeveRetornarFalha(decimal valorInvalido)
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Debito,
            valorInvalido,
            DateTime.Today,
            "Descrição",
            "COM001");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.ValorInvalido");
    }

    [Fact]
    public void Criar_ComDescricaoVazia_DeveRetornarFalha()
    {
        // Arrange & Act
        var resultado = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "",
            "COM001");

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.DescricaoObrigatoria");
    }

    [Fact]
    public void Confirmar_LancamentoPendente_DeveAlterarStatusParaConfirmado()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Value;

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.IsSuccess.Should().BeTrue();
        lancamento.Status.Should().Be(EnumStatusLancamento.Confirmado);
    }

    [Fact]
    public void Confirmar_LancamentoJaConfirmado_DeveRetornarFalha()
    {
        // Arrange
        var lancamento = Lancamento.Criar(
            EnumTipoLancamento.Credito,
            100m,
            DateTime.Today,
            "Teste",
            "COM001").Value;

        lancamento.Confirmar();

        // Act
        var resultado = lancamento.Confirmar();

        // Assert
        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Codigo.Should().Be("Lancamento.JaConfirmado");
    }
}
```

## Critérios de Aceitação

- [ ] Agregado `Lancamento` implementado com validações de domínio
- [ ] Repository pattern implementado com EF Core
- [ ] DbContext configurado com PostgreSQL
- [ ] Handlers de comandos implementados com Rebus
- [ ] API REST com endpoints de lançamentos
- [ ] Migrations criadas e aplicadas
- [ ] Testes unitários com cobertura > 80%
- [ ] Logs estruturados em todas as operações
- [ ] Tratamento de erros com Result Pattern
- [ ] Docker Compose configurado para PostgreSQL

## Trade-offs

**Benefícios:**
- Domínio rico com validações encapsuladas
- Separação clara de responsabilidades
- Código testável e testado (TDD)
- Rastreabilidade completa via eventos
- Performance otimizada com índices

**Considerações:**
- Complexidade adicional do DDD
- Curva de aprendizado para Result Pattern
- Overhead de eventos de domínio

## Estimativa

**Tempo Total**: 8-12 horas

- Implementação do domínio: 2-3 horas
- Repositórios e persistência: 2-3 horas
- Handlers e mensageria: 2 horas
- API REST: 1-2 horas
- Testes unitários (TDD): 3-4 horas
- Configuração e documentação: 1 hora

---

**Versão**: 1.0
**Data de criação**: 2026-01-15
**Última atualização**: 2026-01-15
