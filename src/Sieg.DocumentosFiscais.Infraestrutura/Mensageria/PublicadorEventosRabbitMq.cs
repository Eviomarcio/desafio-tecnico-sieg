using System.Text;
using RabbitMQ.Client;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Mensageria;

public sealed class PublicadorEventosRabbitMq(OpcoesRabbitMq opcoes)
    : IPublicadorEventos, IAsyncDisposable
{
    private readonly SemaphoreSlim _bloqueio = new(1, 1);
    private IConnection? _conexao;
    private IChannel? _canal;

    public async Task PublicarAsync(
        EventoPendente evento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        await _bloqueio.WaitAsync(cancellationToken);
        try
        {
            var canal = await ObterCanalAsync(cancellationToken);
            var propriedades = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                Persistent = true,
                MessageId = evento.Id.ToString("D"),
                Type = evento.Tipo,
                Timestamp = new AmqpTimestamp(evento.CriadoEm.ToUnixTimeSeconds())
            };

            await canal.BasicPublishAsync(
                opcoes.NomeExchange,
                opcoes.ChaveRoteamento,
                mandatory: true,
                basicProperties: propriedades,
                body: Encoding.UTF8.GetBytes(evento.Conteudo),
                cancellationToken: cancellationToken);
        }
        catch
        {
            await DescartarConexaoAsync();
            throw;
        }
        finally
        {
            _bloqueio.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _bloqueio.WaitAsync();
        try
        {
            await DescartarConexaoAsync();
        }
        finally
        {
            _bloqueio.Release();
        }

        _bloqueio.Dispose();
    }

    private async Task<IChannel> ObterCanalAsync(CancellationToken cancellationToken)
    {
        if (_canal is { IsOpen: true })
        {
            return _canal;
        }

        await DescartarConexaoAsync();

        var fabrica = new ConnectionFactory
        {
            HostName = opcoes.Servidor,
            Port = opcoes.Porta,
            UserName = opcoes.Usuario,
            Password = opcoes.Senha,
            VirtualHost = opcoes.HostVirtual,
            ClientProvidedName = "sieg-documentos-fiscais-publicador",
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _conexao = await fabrica.CreateConnectionAsync(cancellationToken);
        var opcoesCanal = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);
        _canal = await _conexao.CreateChannelAsync(opcoesCanal, cancellationToken);

        await _canal.ExchangeDeclareAsync(
            opcoes.NomeExchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _canal.QueueDeclareAsync(
            opcoes.NomeFila,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await _canal.QueueBindAsync(
            opcoes.NomeFila,
            opcoes.NomeExchange,
            opcoes.ChaveRoteamento,
            cancellationToken: cancellationToken);

        return _canal;
    }

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
