using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Testes.Integracao.Infraestrutura;
using Testcontainers.PostgreSql;

namespace Sieg.DocumentosFiscais.Testes.Integracao.Api;

[TestFixture]
[NonParallelizable]
public sealed class DocumentosFiscaisApiTestes : IAsyncDisposable
{
    private const string RotaDocumentos = "/api/v1/documentos-fiscais";

    private static readonly JsonSerializerOptions OpcoesJson = CriarOpcoesJson();

    private readonly PostgreSqlContainer _postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("documentos_fiscais_testes")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private FabricaApi _fabrica = null!;
    private HttpClient _cliente = null!;
    private bool _inicializado;

    [OneTimeSetUp]
    public async Task IniciarAsync()
    {
        await _postgreSql.StartAsync();
        _fabrica = new FabricaApi(_postgreSql.GetConnectionString());
        _cliente = _fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        _inicializado = true;
    }

    [SetUp]
    public Task PrepararBancoAsync() => _fabrica.RecriarBancoAsync();

    [OneTimeTearDown]
    public async Task EncerrarAsync() => await DisposeAsync();

    [TestCaseSource(nameof(DocumentosFiscaisSuportados))]
    public async Task Processar_DocumentoSuportado_DevePersistirDocumentoEEventoOutbox(
        string xml,
        TipoDocumentoFiscal tipoEsperado)
    {
        using var resposta = await EnviarXmlAsync(HttpMethod.Post, RotaDocumentos, xml);
        var documento = await resposta.Content.ReadFromJsonAsync<DocumentoFiscalDetalhesDto>(
            OpcoesJson);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resposta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(documento, Is.Not.Null);
            Assert.That(documento!.Tipo, Is.EqualTo(tipoEsperado));
            Assert.That(resposta.Headers.Location, Is.Not.Null);
        }

        var totais = await _fabrica.ConsultarBancoAsync(async contexto => new
        {
            Documentos = await contexto.DocumentosFiscais.CountAsync(),
            Eventos = await contexto.EventosPendentes.CountAsync(),
            ConteudoEvento = await contexto.EventosPendentes
                .Select(evento => evento.Conteudo)
                .SingleAsync()
        });
        using var eventoJson = JsonDocument.Parse(totais.ConteudoEvento);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(totais.Documentos, Is.EqualTo(1));
            Assert.That(totais.Eventos, Is.EqualTo(1));
            Assert.That(
                eventoJson.RootElement.GetProperty("Acao").GetInt32(),
                Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Processar_MesmoXmlDuasVezes_DeveSerIdempotente()
    {
        using var primeiraResposta = await EnviarXmlAsync(
            HttpMethod.Post,
            RotaDocumentos,
            XmlNFe);
        using var segundaResposta = await EnviarXmlAsync(
            HttpMethod.Post,
            RotaDocumentos,
            XmlNFe);
        var primeiroDocumento = await primeiraResposta.Content
            .ReadFromJsonAsync<DocumentoFiscalDetalhesDto>(OpcoesJson);
        var segundoDocumento = await segundaResposta.Content
            .ReadFromJsonAsync<DocumentoFiscalDetalhesDto>(OpcoesJson);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(primeiraResposta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(segundaResposta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(
                segundaResposta.Headers.GetValues("Idempotent-Replay"),
                Does.Contain("true"));
            Assert.That(segundoDocumento!.Id, Is.EqualTo(primeiroDocumento!.Id));
        }

        var totais = await _fabrica.ConsultarBancoAsync(async contexto => new
        {
            Documentos = await contexto.DocumentosFiscais.CountAsync(),
            Eventos = await contexto.EventosPendentes.CountAsync()
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(totais.Documentos, Is.EqualTo(1));
            Assert.That(totais.Eventos, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Listar_ComFiltrosEPaginacao_DeveRetornarSomenteDocumentosCorrespondentes()
    {
        using var respostaNFe = await EnviarXmlAsync(HttpMethod.Post, RotaDocumentos, XmlNFe);
        using var respostaCTe = await EnviarXmlAsync(HttpMethod.Post, RotaDocumentos, XmlCTe);
        respostaNFe.EnsureSuccessStatusCode();
        respostaCTe.EnsureSuccessStatusCode();

        using var resposta = await _cliente.GetAsync(
            $"{RotaDocumentos}?tipo=NFe&cnpj=12.345.678%2F0001-95" +
            "&unidadeFederativa=SP&pagina=1&tamanhoPagina=1");
        var pagina = await resposta.Content
            .ReadFromJsonAsync<PaginaResultado<DocumentoFiscalResumoDto>>(OpcoesJson);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resposta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(pagina, Is.Not.Null);
            Assert.That(pagina!.TotalItens, Is.EqualTo(1));
            Assert.That(pagina.TotalPaginas, Is.EqualTo(1));
            Assert.That(pagina.Itens, Has.Count.EqualTo(1));
            Assert.That(pagina.Itens.Single().Tipo, Is.EqualTo(TipoDocumentoFiscal.NFe));
            Assert.That(pagina.Itens.Single().CnpjEmitente, Is.EqualTo("12.***.***/0001-**"));
        }
    }

    [Test]
    public async Task ConsultarAtualizarEExcluir_DeveCompletarCicloDoDocumento()
    {
        using var respostaCriacao = await EnviarXmlAsync(
            HttpMethod.Post,
            RotaDocumentos,
            XmlNFe);
        var criado = await respostaCriacao.Content
            .ReadFromJsonAsync<DocumentoFiscalDetalhesDto>(OpcoesJson);

        using var respostaConsulta = await _cliente.GetAsync($"{RotaDocumentos}/{criado!.Id}");
        using var respostaAtualizacao = await EnviarXmlAsync(
            HttpMethod.Put,
            $"{RotaDocumentos}/{criado.Id}",
            XmlCTe);
        var atualizado = await respostaAtualizacao.Content
            .ReadFromJsonAsync<DocumentoFiscalDetalhesDto>(OpcoesJson);
        using var respostaExclusao = await _cliente.DeleteAsync($"{RotaDocumentos}/{criado.Id}");
        using var respostaDepoisDaExclusao = await _cliente.GetAsync(
            $"{RotaDocumentos}/{criado.Id}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(respostaConsulta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(respostaAtualizacao.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(atualizado!.Tipo, Is.EqualTo(TipoDocumentoFiscal.CTe));
            Assert.That(respostaExclusao.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(respostaDepoisDaExclusao.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }

        var quantidadeEventos = await _fabrica.ConsultarBancoAsync(contexto =>
            contexto.EventosPendentes.CountAsync());
        Assert.That(quantidadeEventos, Is.EqualTo(2));
    }

    [Test]
    public async Task Processar_XmlMalformado_DeveRetornarProblemaSemPersistirDados()
    {
        using var resposta = await EnviarXmlAsync(
            HttpMethod.Post,
            RotaDocumentos,
            "<NFe><infNFe></NFe>");
        var problema = await resposta.Content.ReadFromJsonAsync<ProblemDetails>(OpcoesJson);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resposta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(problema!.Title, Is.EqualTo("XML fiscal inválido"));
            Assert.That(problema.Extensions, Contains.Key("traceId"));
        }

        var quantidadeDocumentos = await _fabrica.ConsultarBancoAsync(contexto =>
            contexto.DocumentosFiscais.CountAsync());
        Assert.That(quantidadeDocumentos, Is.Zero);
    }

    [Test]
    public async Task ProcessarEConsultar_NaoDevemExporXmlOuCnpjsSemMascara()
    {
        using var respostaCriacao = await EnviarXmlAsync(
            HttpMethod.Post,
            RotaDocumentos,
            XmlNFe);
        var corpoCriacao = await respostaCriacao.Content.ReadAsStringAsync();
        var documento = await respostaCriacao.Content
            .ReadFromJsonAsync<DocumentoFiscalDetalhesDto>(OpcoesJson);

        using var respostaConsulta = await _cliente.GetAsync(
            $"{RotaDocumentos}/{documento!.Id}");
        var corpoConsulta = await respostaConsulta.Content.ReadAsStringAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(respostaCriacao.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(respostaConsulta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            AssertRespostaProtegida(corpoCriacao);
            AssertRespostaProtegida(corpoConsulta);
        }

        var conteudoPersistido = await _fabrica.ConsultarBancoAsync(contexto =>
            contexto.DocumentosFiscais
                .Select(item => item.ConteudoXml)
                .SingleAsync());
        Assert.That(conteudoPersistido, Does.Contain("<nfeProc"));
    }

    [Test]
    public async Task Processar_XmlNaoSuportado_NaoDeveRepetirConteudoNaResposta()
    {
        const string dadoSensivel = "DADO_SIGILOSO_98765432000110";
        var xml = $"<{dadoSensivel}><valor>segredo</valor></{dadoSensivel}>";

        using var resposta = await EnviarXmlAsync(HttpMethod.Post, RotaDocumentos, xml);
        var corpo = await resposta.Content.ReadAsStringAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resposta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity));
            Assert.That(corpo, Does.Not.Contain(dadoSensivel));
            Assert.That(corpo, Does.Not.Contain("segredo"));
        }
    }

    [Test]
    public async Task Listar_ComPaginacaoInvalida_DeveRetornarErroDeValidacao()
    {
        using var resposta = await _cliente.GetAsync(
            $"{RotaDocumentos}?pagina=0&tamanhoPagina=101");
        var problema = await resposta.Content
            .ReadFromJsonAsync<ValidationProblemDetails>(OpcoesJson);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resposta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problema, Is.Not.Null);
            Assert.That(problema!.Errors, Contains.Key("Pagina"));
            Assert.That(problema.Errors, Contains.Key("TamanhoPagina"));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_inicializado)
        {
            return;
        }

        _cliente.Dispose();
        await _fabrica.DisposeAsync();
        await _postgreSql.DisposeAsync();
        _inicializado = false;
    }

    private async Task<HttpResponseMessage> EnviarXmlAsync(
        HttpMethod metodo,
        string rota,
        string xml)
    {
        using var requisicao = new HttpRequestMessage(metodo, rota);
        var formulario = new MultipartFormDataContent();
        var arquivo = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        formulario.Add(arquivo, "arquivo", "documento.xml");
        requisicao.Content = formulario;

        return await _cliente.SendAsync(requisicao);
    }

    private static IEnumerable<TestCaseData> DocumentosFiscaisSuportados()
    {
        yield return new TestCaseData(XmlNFe, TipoDocumentoFiscal.NFe)
            .SetName("Processar_NFe_DevePersistirDocumentoEEventoOutbox");
        yield return new TestCaseData(XmlCTe, TipoDocumentoFiscal.CTe)
            .SetName("Processar_CTe_DevePersistirDocumentoEEventoOutbox");
        yield return new TestCaseData(XmlNFSe, TipoDocumentoFiscal.NFSe)
            .SetName("Processar_NFSe_DevePersistirDocumentoEEventoOutbox");
    }

    private static JsonSerializerOptions CriarOpcoesJson()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new JsonStringEnumConverter());
        return opcoes;
    }

    private static void AssertRespostaProtegida(string resposta)
    {
        Assert.That(resposta, Does.Not.Contain("conteudoXml").IgnoreCase);
        Assert.That(resposta, Does.Not.Contain("<nfeProc").IgnoreCase);
        Assert.That(resposta, Does.Not.Contain("12345678000195"));
        Assert.That(resposta, Does.Not.Contain("98765432000110"));
        Assert.That(resposta, Does.Contain("12.***.***/0001-**"));
        Assert.That(resposta, Does.Contain("98.***.***/0001-**"));
    }

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

    private const string XmlCTe = """
        <cteProc xmlns="http://www.portalfiscal.inf.br/cte">
          <CTe>
            <infCte Id="CTe41261022345678000195570010000000011000000010">
              <ide><dhEmi>2026-10-02T10:00:00-03:00</dhEmi></ide>
              <emit><CNPJ>22.345.678/0001-95</CNPJ><UF>PR</UF></emit>
              <dest><CNPJ>88.765.432/0001-10</CNPJ></dest>
            </infCte>
          </CTe>
        </cteProc>
        """;

    private const string XmlNFSe = """
        <CompNfse>
          <Nfse>
            <InfNfse Id="NFSE-0001">
              <DataEmissao>2026-10-03T11:00:00-03:00</DataEmissao>
              <PrestadorServico><Cnpj>32.345.678/0001-95</Cnpj><Uf>SC</Uf></PrestadorServico>
              <TomadorServico><Cnpj>78.765.432/0001-10</Cnpj></TomadorServico>
            </InfNfse>
          </Nfse>
        </CompNfse>
        """;
}
