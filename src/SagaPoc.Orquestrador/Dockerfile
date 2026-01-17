# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto
COPY ["src/SagaPoc.Orquestrador/SagaPoc.Orquestrador.csproj", "SagaPoc.Orquestrador/"]
COPY ["src/BuildingBlocks/SagaPoc.Common/SagaPoc.Common.csproj", "BuildingBlocks/SagaPoc.Common/"]
COPY ["src/BuildingBlocks/SagaPoc.Observability/SagaPoc.Observability.csproj", "BuildingBlocks/SagaPoc.Observability/"]
COPY ["src/BuildingBlocks/SagaPoc.Infrastructure/SagaPoc.Infrastructure.csproj", "BuildingBlocks/SagaPoc.Infrastructure/"]
COPY ["src/BuildingBlocks/SagaPoc.Infrastructure.Core/SagaPoc.Infrastructure.Core.csproj", "BuildingBlocks/SagaPoc.Infrastructure.Core/"]

# Restaurar dependências
RUN dotnet restore "SagaPoc.Orquestrador/SagaPoc.Orquestrador.csproj"

# Copiar todo o código fonte
COPY src/SagaPoc.Orquestrador/. SagaPoc.Orquestrador/
COPY src/BuildingBlocks/SagaPoc.Common/. BuildingBlocks/SagaPoc.Common/
COPY src/BuildingBlocks/SagaPoc.Observability/. BuildingBlocks/SagaPoc.Observability/
COPY src/BuildingBlocks/SagaPoc.Infrastructure/. BuildingBlocks/SagaPoc.Infrastructure/
COPY src/BuildingBlocks/SagaPoc.Infrastructure.Core/. BuildingBlocks/SagaPoc.Infrastructure.Core/

# Build da aplicação
WORKDIR /src/SagaPoc.Orquestrador
RUN dotnet build "SagaPoc.Orquestrador.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SagaPoc.Orquestrador.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copiar arquivos publicados
COPY --from=publish /app/publish .

# Definir variáveis de ambiente
ENV ASPNETCORE_ENVIRONMENT=Development

# Executar aplicação
ENTRYPOINT ["dotnet", "SagaPoc.Orquestrador.dll"]
