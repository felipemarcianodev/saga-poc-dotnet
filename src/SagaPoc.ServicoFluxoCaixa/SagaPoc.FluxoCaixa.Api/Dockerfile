# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto
COPY ["src/SagaPoc.FluxoCaixa.Api/SagaPoc.FluxoCaixa.Api.csproj", "src/SagaPoc.FluxoCaixa.Api/"]
COPY ["src/SagaPoc.FluxoCaixa.Domain/SagaPoc.FluxoCaixa.Domain.csproj", "src/SagaPoc.FluxoCaixa.Domain/"]
COPY ["src/SagaPoc.FluxoCaixa.Infrastructure/SagaPoc.FluxoCaixa.Infrastructure.csproj", "src/SagaPoc.FluxoCaixa.Infrastructure/"]
COPY ["src/SagaPoc.FluxoCaixa.Lancamentos/SagaPoc.FluxoCaixa.Lancamentos.csproj", "src/SagaPoc.FluxoCaixa.Lancamentos/"]
COPY ["src/SagaPoc.Shared/SagaPoc.Shared.csproj", "src/SagaPoc.Shared/"]

# Restaurar dependências
RUN dotnet restore "src/SagaPoc.FluxoCaixa.Api/SagaPoc.FluxoCaixa.Api.csproj"

# Copiar código fonte
COPY . .

# Build
WORKDIR "/src/src/SagaPoc.FluxoCaixa.Api"
RUN dotnet build "SagaPoc.FluxoCaixa.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "SagaPoc.FluxoCaixa.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SagaPoc.FluxoCaixa.Api.dll"]
