# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia o src inteiro (bin/obj ficam fora pelo .dockerignore). Evita a lista
# manual de projetos, que ficava defasada a cada projeto criado ou removido.
# O Directory.Build.props da raiz define o TFM de todos os projetos.
COPY Directory.Build.props ./
COPY src/ src/

WORKDIR /src/src/SagaPoc.ServicoEntregador
RUN dotnet restore "SagaPoc.ServicoEntregador.csproj"

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SagaPoc.ServicoEntregador.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=publish /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "SagaPoc.ServicoEntregador.dll"]
