using System.Text.Json;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Testes.Unitarios.Utilitarios;

namespace Sieg.DocumentosFiscais.Testes.Unitarios.Aplicacao;

[TestFixture]
public sealed class ServicoDocumentosFiscaisTestes
{
    private IDocumentoFiscalRepositorio _documentoRepositorio = null!;
    private IEventoPendenteRepositorio _eventoPendenteRepositorio = null!;
    private IProcessadorXmlFiscal _processadorXml = null!;
    private IUnidadeTrabalho _unidadeTrabalho = null!;
    private ServicoDocumentosFiscais _servico = null!;

    [SetUp]
    public void Preparar()
    {
        _documentoRepositorio = Substitute.For<IDocumentoFiscalRepositorio>();
        _eventoPendenteRepositorio = Substitute.For<IEventoPendenteRepositorio>();
        _processadorXml = Substitute.For<IProcessadorXmlFiscal>();
        _unidadeTrabalho = Substitute.For<IUnidadeTrabalho>();
        _servico = new ServicoDocumentosFiscais(
            _documentoRepositorio,
            _eventoPendenteRepositorio,
            _processadorXml,
            _unidadeTrabalho,
            new ProvedorTempoFixo(FabricaObjetosTeste.Instante));
    }

    [Test]
    public async Task Processar_QuandoDocumentoForNovo_DeveSalvarDocumentoEEventoNaMesmaUnidadeTrabalho()
    {
        var processado = FabricaObjetosTeste.CriarDocumentoProcessado();
        _processadorXml.Processar(Arg.Any<ReadOnlyMemory<byte>>()).Returns(processado);
        _documentoRepositorio
            .ObterPorHashAsync(processado.HashConteudo, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DocumentoFiscal?>(null));
        _documentoRepositorio
            .ObterPorChaveFiscalAsync(processado.Tipo, processado.ChaveFiscal!, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DocumentoFiscal?>(null));
        DocumentoFiscal? documentoAdicionado = null;
        EventoPendente? eventoAdicionado = null;
        _documentoRepositorio
            .AdicionarAsync(Arg.Do<DocumentoFiscal>(item => documentoAdicionado = item), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _eventoPendenteRepositorio
            .AdicionarAsync(Arg.Do<EventoPendente>(item => eventoAdicionado = item), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var resultado = await _servico.ProcessarAsync(
            "xml"u8.ToArray(),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resultado.FoiCriado, Is.True);
            Assert.That(documentoAdicionado, Is.Not.Null);
            Assert.That(eventoAdicionado, Is.Not.Null);
            Assert.That(eventoAdicionado!.Tipo, Is.EqualTo("DocumentoFiscalProcessadoEvento"));
            Assert.That(eventoAdicionado.Conteudo, Does.Contain("\"Acao\":1"));
        }
        await _unidadeTrabalho.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Processar_QuandoHashJaExistir_DeveRetornarDocumentoSemNovaGravacao()
    {
        var processado = FabricaObjetosTeste.CriarDocumentoProcessado();
        var existente = FabricaObjetosTeste.CriarDocumento();
        _processadorXml.Processar(Arg.Any<ReadOnlyMemory<byte>>()).Returns(processado);
        _documentoRepositorio
            .ObterPorHashAsync(processado.HashConteudo, Arg.Any<CancellationToken>())
            .Returns(existente);

        var resultado = await _servico.ProcessarAsync(
            "xml"u8.ToArray(),
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resultado.FoiCriado, Is.False);
            Assert.That(resultado.Documento.Id, Is.EqualTo(existente.Id));
        }
        await _documentoRepositorio.DidNotReceiveWithAnyArgs()
            .AdicionarAsync(default!, default);
        await _eventoPendenteRepositorio.DidNotReceiveWithAnyArgs()
            .AdicionarAsync(default!, default);
        await _unidadeTrabalho.DidNotReceiveWithAnyArgs()
            .SalvarAlteracoesAsync(default);
    }

    [Test]
    public void Processar_QuandoChavePertencerAOutroConteudo_DeveLancarConflito()
    {
        var processado = FabricaObjetosTeste.CriarDocumentoProcessado(FabricaObjetosTeste.HashB);
        _processadorXml.Processar(Arg.Any<ReadOnlyMemory<byte>>()).Returns(processado);
        _documentoRepositorio
            .ObterPorHashAsync(processado.HashConteudo, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DocumentoFiscal?>(null));
        _documentoRepositorio
            .ObterPorChaveFiscalAsync(processado.Tipo, processado.ChaveFiscal!, Arg.Any<CancellationToken>())
            .Returns(FabricaObjetosTeste.CriarDocumento());

        var excecao = Assert.ThrowsAsync<DocumentoFiscalConflitoException>(
            (Func<Task>)(() =>
                _servico.ProcessarAsync("xml"u8.ToArray(), CancellationToken.None)));

        Assert.That(excecao!.Message, Does.Contain("mesmo tipo e chave fiscal"));
    }

    [Test]
    public async Task Listar_DeveNormalizarPaginacaoEMascararCnpj()
    {
        var filtro = new FiltroDocumentosFiscais(
            Tipo: null,
            Cnpj: null,
            UnidadeFederativa: null,
            DataEmissaoInicial: null,
            DataEmissaoFinal: null,
            Pagina: 0,
            TamanhoPagina: 200);
        _documentoRepositorio
            .ListarAsync(filtro, Arg.Any<CancellationToken>())
            .Returns((new[] { FabricaObjetosTeste.CriarDocumento() }, 201));

        var pagina = await _servico.ListarAsync(filtro, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pagina.Pagina, Is.EqualTo(1));
            Assert.That(pagina.TamanhoPagina, Is.EqualTo(100));
            Assert.That(pagina.TotalPaginas, Is.EqualTo(3));
            Assert.That(pagina.Itens.Single().CnpjEmitente, Is.EqualTo("12.***.***/0001-**"));
        }
    }

    [Test]
    public void ObterPorId_QuandoNaoExistir_DeveLancarNaoEncontrado()
    {
        var id = Guid.NewGuid();
        _documentoRepositorio
            .ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DocumentoFiscal?>(null));

        var excecao = Assert.ThrowsAsync<DocumentoFiscalNaoEncontradoException>(
            (Func<Task>)(() =>
                _servico.ObterPorIdAsync(id, CancellationToken.None)));

        Assert.That(excecao!.Message, Does.Contain(id.ToString()));
    }

    [Test]
    public async Task ObterPorId_QuandoExistir_DeveMascararDadosSensiveisEOmitirXml()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();
        _documentoRepositorio
            .ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);

        var resultado = await _servico.ObterPorIdAsync(
            documento.Id,
            CancellationToken.None);
        var respostaJson = JsonSerializer.Serialize(resultado);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resultado.CnpjEmitente, Is.EqualTo("12.***.***/0001-**"));
            Assert.That(resultado.CnpjDestinatario, Is.EqualTo("98.***.***/0001-**"));
            Assert.That(resultado.ChaveFiscal, Does.Not.Contain("12345678000195"));
            Assert.That(respostaJson, Does.Not.Contain(documento.ConteudoXml));
            Assert.That(respostaJson, Does.Not.Contain("12345678000195"));
            Assert.That(respostaJson, Does.Not.Contain("98765432000110"));
            Assert.That(respostaJson, Does.Not.Contain("ConteudoXml"));
        }
    }

    [Test]
    public async Task ObterPorId_ComCnpjAlfanumerico_DeveAplicarMascara()
    {
        var documento = new DocumentoFiscal(
            TipoDocumentoFiscal.NFSe,
            "NFSE-ALFANUMERICA",
            "AB.CDE.FGH/IJKL-01",
            null,
            "SC",
            FabricaObjetosTeste.Instante,
            FabricaObjetosTeste.HashA,
            "<CompNfse />",
            FabricaObjetosTeste.Instante);
        _documentoRepositorio
            .ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);

        var resultado = await _servico.ObterPorIdAsync(
            documento.Id,
            CancellationToken.None);

        Assert.That(resultado.CnpjEmitente, Is.EqualTo("AB.***.***/IJKL-**"));
    }

    [Test]
    public async Task Excluir_QuandoExistir_DeveRemoverESalvar()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();
        _documentoRepositorio
            .ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);

        await _servico.ExcluirAsync(documento.Id, CancellationToken.None);

        _documentoRepositorio.Received(1).Excluir(documento);
        await _unidadeTrabalho.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
