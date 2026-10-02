FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacao
ARG CONFIGURACAO=Release
WORKDIR /codigo

COPY Directory.Build.props ./
COPY src/Sieg.DocumentosFiscais.Dominio/Sieg.DocumentosFiscais.Dominio.csproj src/Sieg.DocumentosFiscais.Dominio/
COPY src/Sieg.DocumentosFiscais.Aplicacao/Sieg.DocumentosFiscais.Aplicacao.csproj src/Sieg.DocumentosFiscais.Aplicacao/
COPY src/Sieg.DocumentosFiscais.Infraestrutura/Sieg.DocumentosFiscais.Infraestrutura.csproj src/Sieg.DocumentosFiscais.Infraestrutura/
COPY src/Sieg.DocumentosFiscais.Api/Sieg.DocumentosFiscais.Api.csproj src/Sieg.DocumentosFiscais.Api/
RUN dotnet restore src/Sieg.DocumentosFiscais.Api/Sieg.DocumentosFiscais.Api.csproj

COPY src/ src/
RUN dotnet publish src/Sieg.DocumentosFiscais.Api/Sieg.DocumentosFiscais.Api.csproj \
    --configuration $CONFIGURACAO \
    --output /publicacao \
    --no-restore \
    /p:RunAnalyzers=false \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=compilacao /publicacao .
ENTRYPOINT ["dotnet", "Sieg.DocumentosFiscais.Api.dll"]
