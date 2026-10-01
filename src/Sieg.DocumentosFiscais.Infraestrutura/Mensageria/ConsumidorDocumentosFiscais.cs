using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Sieg.DocumentosFiscais.Aplicacao.Eventos;
using Sieg.DocumentosFiscais.Dominio.Eventos;

namespace Sieg.DocumentosFiscais.Infraestrutura.Mensageria;

public sealed class ConsumidorDocumentosFiscais(
    IServiceScopeFactory fabricaEscopos,
    OpcoesRabbitMq opcoes,
    TimeProvider provedorTempo,
    ILogger<ConsumidorDocumentosFiscais> logger) : BackgroundService
{
    private const string CabecalhoTentativa = "x-tentativa";
    private const string CabecalhoUltimoErro = "x-ultimo-erro";

    private static readonly Action<ILogger, Exception?> RegistrarConsumidorIniciado =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(1, nameof(RegistrarConsumidorIniciado)),
            "Consumidor de documentos fiscais iniciado");

    private static readonly Action<ILogger, Guid, Exception?> RegistrarEventoProcessado =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(2, nameof(RegistrarEventoProcessado)),
            "Evento {EventoId} processado com sucesso");

    private static readonly Action<ILogger, Guid, Exception?> RegistrarEventoDuplicado =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(RegistrarEventoDuplicado)),
            "Evento {EventoId} já havia sido processado e foi confirmado sem repetição");

    private static readonly Action<ILogger, string, int, Exception?> RegistrarRetentativa =
        LoggerMessage.Define<string, int>(
            LogLevel.Warning,
            new EventId(4, nameof(RegistrarRetentativa)),
            "Mensagem {MensagemId} encaminhada para a retentativa {Tentativa}");

    private static readonly Action<ILogger, string, Exception?> RegistrarFilaFalhas =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(5, nameof(RegistrarFilaFalhas)),
            "Mensagem {MensagemId} encaminhada para a fila de falhas");

    private static readonly Action<ILogger, Exception?> RegistrarFalhaConexao =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(6, nameof(RegistrarFalhaConexao)),
            "Falha na conexão do consumidor com o RabbitMQ; uma nova tentativa será realizada");

    private IConnection? _conexao;
    private IChannel? _canal;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await IniciarConsumoAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excecao)
            {
                RegistrarFalhaConexao(logger, excecao);
            }
            finally
            {
                await DescartarConexaoAsync();
            }

            await Task.Delay(TimeSpan.FromSeconds(5), provedorTempo, stoppingToken);
        }
    }

    private async Task IniciarConsumoAsync(CancellationToken cancellationToken)
    {
        var fabrica = new ConnectionFactory
        {
            HostName = opcoes.Servidor,
            Port = opcoes.Porta,
            UserName = opcoes.Usuario,
            Password = opcoes.Senha,
            VirtualHost = opcoes.HostVirtual,
            ClientProvidedName = "sieg-documentos-fiscais-consumidor",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _conexao = await fabrica.CreateConnectionAsync(cancellationToken);
        var opcoesCanal = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true,
            consumerDispatchConcurrency: 1);
        _canal = await _conexao.CreateChannelAsync(opcoesCanal, cancellationToken);

        await DeclararTopologiaAsync(_canal, cancellationToken);
        await _canal.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: checked((ushort)opcoes.LimiteMensagensNaoConfirmadas),
            global: false,
            cancellationToken: cancellationToken);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += ProcessarMensagemAsync;

        await _canal.BasicConsumeAsync(
            opcoes.NomeFila,
            autoAck: false,
            consumidor,
            cancellationToken);
        RegistrarConsumidorIniciado(logger, null);

        await Task.Delay(Timeout.InfiniteTimeSpan, provedorTempo, cancellationToken);
    }

    private async Task ProcessarMensagemAsync(
        object remetente,
        BasicDeliverEventArgs argumentos)
    {
        _ = remetente;
        var canal = _canal
            ?? throw new InvalidOperationException("O canal do consumidor não está disponível.");
        var conteudo = argumentos.Body.ToArray();

        try
        {
            var evento = DesserializarEvento(conteudo, argumentos.BasicProperties.Type);
            await using var escopo = fabricaEscopos.CreateAsyncScope();
            var processador = escopo.ServiceProvider
                .GetRequiredService<IProcessadorEventosDocumentosFiscais>();
            var processado = await processador.ProcessarAsync(
                evento,
                argumentos.CancellationToken);

            await canal.BasicAckAsync(
                argumentos.DeliveryTag,
                multiple: false,
                argumentos.CancellationToken);

            if (processado)
            {
                RegistrarEventoProcessado(logger, evento.IdEvento, null);
            }
            else
            {
                RegistrarEventoDuplicado(logger, evento.IdEvento, null);
            }
        }
        catch (OperationCanceledException) when (argumentos.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            await TratarFalhaAsync(canal, argumentos, conteudo, excecao);
        }
    }

    private async Task TratarFalhaAsync(
        IChannel canal,
        BasicDeliverEventArgs argumentos,
        byte[] conteudo,
        Exception excecao)
    {
        try
        {
            var tentativa = ObterTentativa(argumentos.BasicProperties.Headers) + 1;

            if (tentativa <= opcoes.IntervalosRetentativaSegundos.Length)
            {
                await PublicarAsync(
                    canal,
                    ObterChaveRetentativa(tentativa),
                    opcoes.NomeExchangeRetentativa,
                    argumentos.BasicProperties,
                    conteudo,
                    tentativa,
                    excecao,
                    argumentos.CancellationToken);
                RegistrarRetentativa(
                    logger,
                    ObterIdentificadorMensagem(argumentos.BasicProperties),
                    tentativa,
                    excecao);
            }
            else
            {
                await PublicarAsync(
                    canal,
                    opcoes.ChaveRoteamentoFalhas,
                    opcoes.NomeExchangeFalhas,
                    argumentos.BasicProperties,
                    conteudo,
                    tentativa,
                    excecao,
                    argumentos.CancellationToken);
                RegistrarFilaFalhas(
                    logger,
                    ObterIdentificadorMensagem(argumentos.BasicProperties),
                    excecao);
            }

            await canal.BasicAckAsync(
                argumentos.DeliveryTag,
                multiple: false,
                argumentos.CancellationToken);
        }
        catch (OperationCanceledException) when (argumentos.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception falhaEncaminhamento)
        {
            RegistrarFalhaConexao(logger, falhaEncaminhamento);
            await canal.BasicNackAsync(
                argumentos.DeliveryTag,
                multiple: false,
                requeue: true,
                argumentos.CancellationToken);
        }
    }

    private static DocumentoFiscalProcessadoEvento DesserializarEvento(
        byte[] conteudo,
        string? tipoMensagem)
    {
        if (!string.Equals(
                tipoMensagem,
                nameof(DocumentoFiscalProcessadoEvento),
                StringComparison.Ordinal))
        {
            throw new JsonException("O tipo da mensagem não é suportado pelo consumidor.");
        }

        var evento = JsonSerializer.Deserialize<DocumentoFiscalProcessadoEvento>(conteudo)
            ?? throw new JsonException("O evento recebido está vazio.");

        if (evento.IdEvento == Guid.Empty || evento.DocumentoFiscalId == Guid.Empty)
        {
            throw new JsonException("O evento recebido não possui identificadores válidos.");
        }

        return evento;
    }

    private static async Task PublicarAsync(
        IChannel canal,
        string chaveRoteamento,
        string exchange,
        IReadOnlyBasicProperties propriedadesOriginais,
        byte[] conteudo,
        int tentativa,
        Exception excecao,
        CancellationToken cancellationToken)
    {
        var mensagemErro = excecao.GetBaseException().Message;
        var propriedades = new BasicProperties
        {
            ContentType = propriedadesOriginais.ContentType ?? "application/json",
            ContentEncoding = propriedadesOriginais.ContentEncoding ?? "utf-8",
            Persistent = true,
            MessageId = propriedadesOriginais.MessageId,
            Type = propriedadesOriginais.Type,
            Timestamp = propriedadesOriginais.Timestamp,
            Headers = new Dictionary<string, object?>
            {
                [CabecalhoTentativa] = tentativa,
                [CabecalhoUltimoErro] = Encoding.UTF8.GetBytes(
                    mensagemErro.Length <= 500 ? mensagemErro : mensagemErro[..500])
            }
        };

        await canal.BasicPublishAsync(
            exchange,
            chaveRoteamento,
            mandatory: true,
            basicProperties: propriedades,
            body: conteudo,
            cancellationToken: cancellationToken);
    }

    private async Task DeclararTopologiaAsync(
        IChannel canal,
        CancellationToken cancellationToken)
    {
        await canal.ExchangeDeclareAsync(
            opcoes.NomeExchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(
            opcoes.NomeFila,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await canal.QueueBindAsync(
            opcoes.NomeFila,
            opcoes.NomeExchange,
            opcoes.ChaveRoteamento,
            cancellationToken: cancellationToken);

        await canal.ExchangeDeclareAsync(
            opcoes.NomeExchangeRetentativa,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        for (var indice = 0; indice < opcoes.IntervalosRetentativaSegundos.Length; indice++)
        {
            var tentativa = indice + 1;
            var fila = ObterNomeFilaRetentativa(tentativa);
            var chave = ObterChaveRetentativa(tentativa);
            var argumentos = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = checked(opcoes.IntervalosRetentativaSegundos[indice] * 1_000),
                ["x-dead-letter-exchange"] = opcoes.NomeExchange,
                ["x-dead-letter-routing-key"] = opcoes.ChaveRoteamento
            };

            await canal.QueueDeclareAsync(
                fila,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: argumentos,
                cancellationToken: cancellationToken);
            await canal.QueueBindAsync(
                fila,
                opcoes.NomeExchangeRetentativa,
                chave,
                cancellationToken: cancellationToken);
        }

        await canal.ExchangeDeclareAsync(
            opcoes.NomeExchangeFalhas,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await canal.QueueDeclareAsync(
            opcoes.NomeFilaFalhas,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await canal.QueueBindAsync(
            opcoes.NomeFilaFalhas,
            opcoes.NomeExchangeFalhas,
            opcoes.ChaveRoteamentoFalhas,
            cancellationToken: cancellationToken);
    }

    private static int ObterTentativa(IDictionary<string, object?>? cabecalhos)
    {
        if (cabecalhos is null || !cabecalhos.TryGetValue(CabecalhoTentativa, out var valor))
        {
            return 0;
        }

        return valor switch
        {
            byte numero => numero,
            short numero => numero,
            int numero => numero,
            long numero when numero <= int.MaxValue => (int)numero,
            byte[] texto when int.TryParse(Encoding.UTF8.GetString(texto), out var numero) => numero,
            _ => 0
        };
    }

    private string ObterNomeFilaRetentativa(int tentativa) =>
        $"{opcoes.NomeFila}.retentativa.{tentativa}";

    private string ObterChaveRetentativa(int tentativa) =>
        $"{opcoes.ChaveRoteamento}.retentativa.{tentativa}";

    private static string ObterIdentificadorMensagem(IReadOnlyBasicProperties propriedades) =>
        string.IsNullOrWhiteSpace(propriedades.MessageId)
            ? "sem-identificador"
            : propriedades.MessageId;

    private async Task DescartarConexaoAsync()
    {
        if (_canal is not null)
        {
            await _canal.DisposeAsync();
            _canal = null;
        }

        if (_conexao is not null)
        {
            await _conexao.DisposeAsync();
            _conexao = null;
        }
    }
}
