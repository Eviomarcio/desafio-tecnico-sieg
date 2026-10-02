using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Aplicacao.Eventos;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Dominio.Eventos;
using Sieg.DocumentosFiscais.Infraestrutura.Mensageria;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Sieg.DocumentosFiscais.Testes.Integracao.Mensageria;

[TestFixture]
[NonParallelizable]
public sealed class MensageriaTestes : IAsyncDisposable
{
    private static readonly TimeSpan TempoLimite = TimeSpan.FromSeconds(20);

    private readonly PostgreSqlContainer _postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("documentos_fiscais_mensageria_testes")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:4.3.6-alpine")
        .Build();

    private IHost _aplicacao = null!;
    private string _nomeFila = string.Empty;
    private string _nomeFilaFalhas = string.Empty;
    private bool _containersIniciados;

    [OneTimeSetUp]
    public async Task IniciarContainersAsync()
    {
        await Task.WhenAll(
            _postgreSql.StartAsync(),
            _rabbitMq.StartAsync());
        _containersIniciados = true;
    }

    [SetUp]
    public async Task PrepararAsync()
    {
        var sufixo = Guid.NewGuid().ToString("N");
        _nomeFila = $"documentos-fiscais-testes-{sufixo}";
        _nomeFilaFalhas = $"{_nomeFila}-falhas";
        _aplicacao = CriarAplicacao(sufixo);

        await RecriarBancoAsync();
        await _aplicacao.StartAsync();
        await AguardarTopologiaAsync();
    }

    [TearDown]
    public async Task EncerrarAplicacaoAsync()
    {
        await _aplicacao.StopAsync();
        _aplicacao.Dispose();
    }

    [OneTimeTearDown]
    public async Task EncerrarContainersAsync() => await DisposeAsync();

    [Test]
    public async Task ProcessarDocumento_DevePublicarOutboxEConsumirGerandoResumo()
    {
        var resultado = await ProcessarDocumentoAsync();

        await AguardarCondicaoAsync(
            async () => await ConsultarBancoAsync(async contexto =>
                await contexto.EventosPendentes.AnyAsync(evento => evento.PublicadoEm != null)
                && await contexto.EventosConsumidos.CountAsync() == 1
                && await contexto.ResumosDocumentosFiscais.CountAsync() == 1),
            "O fluxo Outbox, RabbitMQ e consumidor não foi concluído.");

        var estado = await ConsultarBancoAsync(async contexto => new
        {
            EventoPublicado = await contexto.EventosPendentes
                .Select(evento => evento.PublicadoEm)
                .SingleAsync(),
            QuantidadeConsumidos = await contexto.EventosConsumidos.CountAsync(),
            Resumo = await contexto.ResumosDocumentosFiscais
                .Select(resumo => resumo.Descricao)
                .SingleAsync()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resultado.FoiCriado, Is.True);
            Assert.That(estado.EventoPublicado, Is.Not.Null);
            Assert.That(estado.QuantidadeConsumidos, Is.EqualTo(1));
            Assert.That(estado.Resumo, Does.Contain("Documento NFe"));
            Assert.That(estado.Resumo, Does.Contain("criado"));
        }
    }

    [Test]
    public async Task ConsumirMesmoEventoDuasVezes_DeveManterProcessamentoIdempotente()
    {
        await ProcessarDocumentoAsync();
        await AguardarCondicaoAsync(
            async () => await ConsultarBancoAsync(async contexto =>
                await contexto.EventosConsumidos.CountAsync() == 1
                && await contexto.ResumosDocumentosFiscais.CountAsync() == 1),
            "O primeiro evento não foi consumido.");

        var conteudoEvento = await ConsultarBancoAsync(contexto =>
            contexto.EventosPendentes
                .Select(evento => evento.Conteudo)
                .SingleAsync());
        var eventoDuplicado = new EventoPendente(
            nameof(DocumentoFiscalProcessadoEvento),
            conteudoEvento,
            DateTimeOffset.UtcNow);
        var publicador = _aplicacao.Services.GetRequiredService<IPublicadorEventos>();

        await publicador.PublicarAsync(eventoDuplicado, CancellationToken.None);
        await AguardarFilaVaziaAsync(_nomeFila);
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        var totais = await ConsultarBancoAsync(async contexto => new
        {
            Consumidos = await contexto.EventosConsumidos.CountAsync(),
            Resumos = await contexto.ResumosDocumentosFiscais.CountAsync()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(totais.Consumidos, Is.EqualTo(1));
            Assert.That(totais.Resumos, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task ConsumirEventoInvalido_DeveAplicarRetentativasEEnviarParaFilaFalhas()
    {
        var evento = new DocumentoFiscalProcessadoEvento(
            Guid.NewGuid(),
            Guid.NewGuid(),
            TipoDocumentoFiscal.NFe,
            AcaoDocumentoFiscal.Criado,
            DateTimeOffset.UtcNow);
        var eventoPendente = new EventoPendente(
            nameof(DocumentoFiscalProcessadoEvento),
            JsonSerializer.Serialize(evento),
            DateTimeOffset.UtcNow);
        var publicador = _aplicacao.Services.GetRequiredService<IPublicadorEventos>();

        await publicador.PublicarAsync(eventoPendente, CancellationToken.None);
        var mensagemFalha = await AguardarMensagemAsync(_nomeFilaFalhas);
        var eventoRecebido = JsonSerializer.Deserialize<DocumentoFiscalProcessadoEvento>(
            mensagemFalha.Body.Span);
        var tentativa = ConverterInteiro(
            mensagemFalha.BasicProperties.Headers!["x-tentativa"]);

        var totais = await ConsultarBancoAsync(async contexto => new
        {
            Consumidos = await contexto.EventosConsumidos.CountAsync(),
            Resumos = await contexto.ResumosDocumentosFiscais.CountAsync()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(eventoRecebido, Is.Not.Null);
            Assert.That(eventoRecebido!.IdEvento, Is.EqualTo(evento.IdEvento));
            Assert.That(tentativa, Is.EqualTo(3));
            Assert.That(
                mensagemFalha.BasicProperties.Headers,
                Contains.Key("x-ultimo-erro"));
            Assert.That(totais.Consumidos, Is.Zero);
            Assert.That(totais.Resumos, Is.Zero);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_containersIniciados)
        {
            return;
        }

        await Task.WhenAll(
            _postgreSql.DisposeAsync().AsTask(),
            _rabbitMq.DisposeAsync().AsTask());
        _containersIniciados = false;
    }

    private IHost CriarAplicacao(string sufixo)
    {
        var uriRabbitMq = new Uri(_rabbitMq.GetConnectionString());
        var credenciais = Uri.UnescapeDataString(uriRabbitMq.UserInfo).Split(':', 2);
        var construtor = Host.CreateApplicationBuilder();
        construtor.Configuration.Sources.Clear();
        construtor.Logging.ClearProviders();
        construtor.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PostgreSql"] = _postgreSql.GetConnectionString(),
            ["RabbitMq:Servidor"] = uriRabbitMq.Host,
            ["RabbitMq:Porta"] = uriRabbitMq.Port.ToString(CultureInfo.InvariantCulture),
            ["RabbitMq:Usuario"] = credenciais[0],
            ["RabbitMq:Senha"] = credenciais[1],
            ["RabbitMq:HostVirtual"] = "/",
            ["RabbitMq:NomeExchange"] = $"documentos-fiscais-testes-{sufixo}",
            ["RabbitMq:NomeFila"] = _nomeFila,
            ["RabbitMq:ChaveRoteamento"] = $"documento-fiscal-testes-{sufixo}",
            ["RabbitMq:NomeExchangeRetentativa"] = $"retentativas-testes-{sufixo}",
            ["RabbitMq:IntervalosRetentativaSegundos:0"] = "1",
            ["RabbitMq:IntervalosRetentativaSegundos:1"] = "1",
            ["RabbitMq:NomeExchangeFalhas"] = $"falhas-testes-{sufixo}",
            ["RabbitMq:NomeFilaFalhas"] = _nomeFilaFalhas,
            ["RabbitMq:ChaveRoteamentoFalhas"] = $"falha-testes-{sufixo}",
            ["RabbitMq:LimiteMensagensNaoConfirmadas"] = "1",
            ["RabbitMq:QuantidadeLoteOutbox"] = "20",
            ["RabbitMq:IntervaloPublicacaoSegundos"] = "1"
        });

        construtor.Services.AdicionarPersistencia(construtor.Configuration);
        construtor.Services.AdicionarProcessamentoXml();
        construtor.Services.AdicionarMensageria(construtor.Configuration);
        construtor.Services.AddHostedService<ConsumidorDocumentosFiscais>();
        construtor.Services.AddSingleton(TimeProvider.System);
        construtor.Services.AddScoped<IServicoDocumentosFiscais, ServicoDocumentosFiscais>();
        construtor.Services.AddScoped<
            IProcessadorEventosDocumentosFiscais,
            ProcessadorEventosDocumentosFiscais>();

        return construtor.Build();
    }

    private async Task RecriarBancoAsync()
    {
        await using var escopo = _aplicacao.Services.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<DocumentosFiscaisDbContext>();
        await contexto.Database.EnsureDeletedAsync();
        await contexto.Database.MigrateAsync();
    }

    private async Task<ResultadoProcessamentoDto> ProcessarDocumentoAsync()
    {
        await using var escopo = _aplicacao.Services.CreateAsyncScope();
        var servico = escopo.ServiceProvider.GetRequiredService<IServicoDocumentosFiscais>();
        return await servico.ProcessarAsync(
            Encoding.UTF8.GetBytes(XmlNFe),
            CancellationToken.None);
    }

    private async Task<TResult> ConsultarBancoAsync<TResult>(
        Func<DocumentosFiscaisDbContext, Task<TResult>> consulta)
    {
        await using var escopo = _aplicacao.Services.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<DocumentosFiscaisDbContext>();
        return await consulta(contexto);
    }

    private async Task AguardarTopologiaAsync()
    {
        await AguardarCondicaoAsync(async () =>
        {
            try
            {
                await using var conexao = await CriarConexaoRabbitMqAsync();
                await using var canal = await conexao.CreateChannelAsync();
                await canal.QueueDeclarePassiveAsync(_nomeFila);
                await canal.QueueDeclarePassiveAsync(_nomeFilaFalhas);
                return true;
            }
            catch (Exception excecao)
                when (excecao is BrokerUnreachableException or OperationInterruptedException)
            {
                return false;
            }
        }, "A topologia do RabbitMQ não foi criada.");
    }

    private async Task AguardarFilaVaziaAsync(string nomeFila)
    {
        await AguardarCondicaoAsync(async () =>
        {
            await using var conexao = await CriarConexaoRabbitMqAsync();
            await using var canal = await conexao.CreateChannelAsync();
            var fila = await canal.QueueDeclarePassiveAsync(nomeFila);
            return fila.MessageCount == 0;
        }, $"A fila '{nomeFila}' não foi esvaziada pelo consumidor.");
    }

    private async Task<BasicGetResult> AguardarMensagemAsync(string nomeFila)
    {
        BasicGetResult? mensagem = null;
        await AguardarCondicaoAsync(async () =>
        {
            await using var conexao = await CriarConexaoRabbitMqAsync();
            await using var canal = await conexao.CreateChannelAsync();
            mensagem = await canal.BasicGetAsync(nomeFila, autoAck: true);
            return mensagem is not null;
        }, $"Nenhuma mensagem chegou à fila '{nomeFila}'.");

        return mensagem!;
    }

    private async Task<IConnection> CriarConexaoRabbitMqAsync()
    {
        var fabrica = new ConnectionFactory
        {
            Uri = new Uri(_rabbitMq.GetConnectionString())
        };
        return await fabrica.CreateConnectionAsync();
    }

    private static async Task AguardarCondicaoAsync(
        Func<Task<bool>> condicao,
        string mensagemFalha)
    {
        var cronometro = Stopwatch.StartNew();

        while (cronometro.Elapsed < TempoLimite)
        {
            if (await condicao())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new AssertionException(mensagemFalha);
    }

    private static int ConverterInteiro(object? valor) => valor switch
    {
        byte numero => numero,
        short numero => numero,
        int numero => numero,
        long numero => checked((int)numero),
        byte[] texto => int.Parse(
            Encoding.UTF8.GetString(texto),
            CultureInfo.InvariantCulture),
        _ => throw new AssertionException("O cabeçalho de tentativa possui formato inválido.")
    };

    private const string XmlNFe = """
        <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
          <NFe>
            <infNFe Id="NFe35261012345678000195550010000000011000000010">
              <ide><dhEmi>2026-10-01T09:00:00-03:00</dhEmi></ide>
              <emit><CNPJ>12.345.678/0001-95</CNPJ><UF>SP</UF></emit>
              <dest><CNPJ>98.765.432/0001-10</CNPJ></dest>
            </infNFe>
          </NFe>
        </nfeProc>
        """;
}
