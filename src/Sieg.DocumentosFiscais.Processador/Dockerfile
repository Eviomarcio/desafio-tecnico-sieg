FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
USER $APP_UID

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacao
ARG CONFIGURACAO=Release
WORKDIR /codigo

COPY Directory.Build.props ./
COPY src/Sieg.DocumentosFiscais.Dominio/Sieg.DocumentosFiscais.Dominio.csproj src/Sieg.DocumentosFiscais.Dominio/
COPY src/Sieg.DocumentosFiscais.Aplicacao/Sieg.DocumentosFiscais.Aplicacao.csproj src/Sieg.DocumentosFiscais.Aplicacao/
COPY src/Sieg.DocumentosFiscais.Infraestrutura/Sieg.DocumentosFiscais.Infraestrutura.csproj src/Sieg.DocumentosFiscais.Infraestrutura/
COPY src/Sieg.DocumentosFiscais.Processador/Sieg.DocumentosFiscais.Processador.csproj src/Sieg.DocumentosFiscais.Processador/
RUN dotnet restore src/Sieg.DocumentosFiscais.Processador/Sieg.DocumentosFiscais.Processador.csproj

COPY src/ src/
RUN dotnet publish src/Sieg.DocumentosFiscais.Processador/Sieg.DocumentosFiscais.Processador.csproj \
    --configuration $CONFIGURACAO \
    --output /publicacao \
    --no-restore \
    /p:RunAnalyzers=false \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=compilacao /publicacao .
ENTRYPOINT ["dotnet", "Sieg.DocumentosFiscais.Processador.dll"]
