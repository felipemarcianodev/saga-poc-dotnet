# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto
COPY ["src/SagaPoc.Api/SagaPoc.Api.csproj", "SagaPoc.Api/"]
COPY ["src/SagaPoc.Shared/SagaPoc.Shared.csproj", "SagaPoc.Shared/"]
COPY ["src/WebHost/WebHost.csproj", "WebHost/"]
COPY ["src/SagaPoc.Observability/SagaPoc.Observability.csproj", "SagaPoc.Observability/"]

# Restaurar dependências
RUN dotnet restore "SagaPoc.Api/SagaPoc.Api.csproj"

# Copiar todo o código fonte
COPY src/SagaPoc.Api/. SagaPoc.Api/
COPY src/SagaPoc.Shared/. SagaPoc.Shared/
COPY src/WebHost/. WebHost/
COPY src/SagaPoc.Observability/. SagaPoc.Observability/

# Build da aplicação
WORKDIR /src/SagaPoc.Api
RUN dotnet build "SagaPoc.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SagaPoc.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Expor porta da API
EXPOSE 8080
EXPOSE 8081

# Copiar arquivos publicados
COPY --from=publish /app/publish .

# Definir variáveis de ambiente
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

# Executar aplicação
ENTRYPOINT ["dotnet", "SagaPoc.Api.dll"]
