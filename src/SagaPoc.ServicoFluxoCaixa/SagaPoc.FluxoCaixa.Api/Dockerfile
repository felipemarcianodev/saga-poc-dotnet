# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto mantendo a estrutura de diretórios
COPY ["src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api/SagaPoc.FluxoCaixa.Api.csproj", "src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api/"]
COPY ["src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/SagaPoc.FluxoCaixa.Domain.csproj", "src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/"]
COPY ["src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/SagaPoc.FluxoCaixa.Infrastructure.csproj", "src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/"]
COPY ["src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Lancamentos/SagaPoc.FluxoCaixa.Lancamentos.csproj", "src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Lancamentos/"]
COPY ["src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado/SagaPoc.FluxoCaixa.Consolidado.csproj", "src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado/"]
COPY ["src/BuildingBlocks/SagaPoc.Common/SagaPoc.Common.csproj", "src/BuildingBlocks/SagaPoc.Common/"]
COPY ["src/BuildingBlocks/SagaPoc.Observability/SagaPoc.Observability.csproj", "src/BuildingBlocks/SagaPoc.Observability/"]
COPY ["src/BuildingBlocks/WebHost/WebHost.csproj", "src/BuildingBlocks/WebHost/"]
COPY ["src/BuildingBlocks/SagaPoc.Infrastructure/SagaPoc.Infrastructure.csproj", "src/BuildingBlocks/SagaPoc.Infrastructure/"]
COPY ["src/BuildingBlocks/SagaPoc.Infrastructure.Core/SagaPoc.Infrastructure.Core.csproj", "src/BuildingBlocks/SagaPoc.Infrastructure.Core/"]

# Restaurar dependências
RUN dotnet restore "src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api/SagaPoc.FluxoCaixa.Api.csproj"

# Copiar código fonte mantendo a estrutura
COPY src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api/. src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api/
COPY src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/. src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Domain/
COPY src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/. src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Infrastructure/
COPY src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Lancamentos/. src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Lancamentos/
COPY src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado/. src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Consolidado/
COPY src/BuildingBlocks/SagaPoc.Common/. src/BuildingBlocks/SagaPoc.Common/
COPY src/BuildingBlocks/SagaPoc.Observability/. src/BuildingBlocks/SagaPoc.Observability/
COPY src/BuildingBlocks/WebHost/. src/BuildingBlocks/WebHost/
COPY src/BuildingBlocks/SagaPoc.Infrastructure/. src/BuildingBlocks/SagaPoc.Infrastructure/
COPY src/BuildingBlocks/SagaPoc.Infrastructure.Core/. src/BuildingBlocks/SagaPoc.Infrastructure.Core/

# Build da aplicação
WORKDIR /src/src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api
RUN dotnet build "SagaPoc.FluxoCaixa.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SagaPoc.FluxoCaixa.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Expor porta da API
EXPOSE 8080

# Copiar arquivos publicados
COPY --from=publish /app/publish .

# Definir variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

# Executar aplicação
ENTRYPOINT ["dotnet", "SagaPoc.FluxoCaixa.Api.dll"]
