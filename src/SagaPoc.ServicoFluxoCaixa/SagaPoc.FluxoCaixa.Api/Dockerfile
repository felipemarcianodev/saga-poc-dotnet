# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia o src inteiro (bin/obj ficam fora pelo .dockerignore). Evita a lista
# manual de projetos, que ficava defasada a cada projeto criado ou removido.
COPY src/ src/

WORKDIR /src/src/SagaPoc.ServicoFluxoCaixa/SagaPoc.FluxoCaixa.Api
RUN dotnet restore "SagaPoc.FluxoCaixa.Api.csproj"

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SagaPoc.FluxoCaixa.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

EXPOSE 8080

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "SagaPoc.FluxoCaixa.Api.dll"]
