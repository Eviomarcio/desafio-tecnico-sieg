# Configuração dos ambientes

A API e o processador seguem a precedência padrão de configuração do .NET. Em ambientes
fora de desenvolvimento, os dados de conexão devem ser fornecidos por variáveis de ambiente.
O separador hierárquico usado nos nomes é `__`.

Variáveis obrigatórias:

- `ConnectionStrings__PostgreSql`
- `RabbitMq__Servidor`
- `RabbitMq__Porta`
- `RabbitMq__Usuario`
- `RabbitMq__Senha`

As demais opções operacionais do RabbitMQ permanecem em `appsettings.json` e também podem
ser sobrescritas por variáveis de ambiente, por exemplo:

- `RabbitMq__HostVirtual`
- `RabbitMq__IntervalosRetentativaSegundos__0`
- `RabbitMq__QuantidadeLoteOutbox`
- `RabbitMq__IntervaloPublicacaoSegundos`

Os arquivos `appsettings.Development.json` contêm somente valores locais de conveniência.
Eles não devem ser reutilizados como credenciais de outros ambientes.

Para os contêineres locais, copie `.env.example` para `.env` e ajuste as senhas. O arquivo
`.env` está ignorado pelo Git. Os valores padrão do `docker-compose.yml` existem apenas para
facilitar a execução local do desafio.

## Execução completa com Docker

O Compose constrói e inicia quatro serviços:

- `postgres`: banco de dados;
- `rabbitmq`: broker e interface de gerenciamento;
- `api`: API HTTP, publicação Outbox e aplicação das migrations;
- `processador`: consumidor das mensagens fiscais.

Para construir as imagens e iniciar todo o ambiente:

```powershell
docker compose up --build -d
```

Após os serviços ficarem saudáveis:

- Swagger: `http://localhost:5119/swagger`
- Saúde da API: `http://localhost:5119/saude`
- Gerenciamento do RabbitMQ: `http://localhost:15672`

A porta da API pode ser alterada pela variável `API_PORT`. A API aplica as migrations ao
iniciar apenas quando `BancoDados__AplicarMigracoesAoIniciar` estiver habilitada; o Compose
define essa opção para o ambiente local. Para encerrar os serviços sem apagar os volumes:

```powershell
docker compose down
```
