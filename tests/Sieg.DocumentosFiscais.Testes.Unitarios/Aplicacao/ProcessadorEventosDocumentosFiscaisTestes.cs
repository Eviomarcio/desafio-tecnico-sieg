using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.Eventos;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Dominio.Eventos;
using Sieg.DocumentosFiscais.Testes.Unitarios.Utilitarios;

namespace Sieg.DocumentosFiscais.Testes.Unitarios.Aplicacao;

[TestFixture]
public sealed class ProcessadorEventosDocumentosFiscaisTestes
{
    private IDocumentoFiscalRepositorio _documentoRepositorio = null!;
    private IResumoDocumentoFiscalRepositorio _resumoRepositorio = null!;
    private IEventoConsumidoRepositorio _eventoConsumidoRepositorio = null!;
    private IUnidadeTrabalho _unidadeTrabalho = null!;
    private ProcessadorEventosDocumentosFiscais _processador = null!;

    [SetUp]
    public void Preparar()
    {
        _documentoRepositorio = Substitute.For<IDocumentoFiscalRepositorio>();
        _resumoRepositorio = Substitute.For<IResumoDocumentoFiscalRepositorio>();
        _eventoConsumidoRepositorio = Substitute.For<IEventoConsumidoRepositorio>();
        _unidadeTrabalho = Substitute.For<IUnidadeTrabalho>();
        _processador = new ProcessadorEventosDocumentosFiscais(
            _documentoRepositorio,
            _resumoRepositorio,
            _eventoConsumidoRepositorio,
            _unidadeTrabalho,
            new ProvedorTempoFixo(FabricaObjetosTeste.Instante));
    }

    [Test]
    public async Task Processar_QuandoEventoJaConsumido_DeveIgnorarSemEfeitosColaterais()
    {
        var evento = CriarEvento(Guid.NewGuid());
        _eventoConsumidoRepositorio
            .ExisteAsync(
                evento.IdEvento,
                ProcessadorEventosDocumentosFiscais.NomeConsumidor,
                Arg.Any<CancellationToken>())
            .Returns(true);

        var foiProcessado = await _processador.ProcessarAsync(
            evento,
            CancellationToken.None);

        Assert.That(foiProcessado, Is.False);
        await _documentoRepositorio.DidNotReceiveWithAnyArgs()
            .ObterPorIdAsync(default, default);
        await _unidadeTrabalho.DidNotReceiveWithAnyArgs()
            .SalvarAlteracoesAsync(default);
    }

    [Test]
    public async Task Processar_QuandoForNovo_DeveCriarResumoERegistrarIdempotencia()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();
        var evento = CriarEvento(documento.Id);
        _eventoConsumidoRepositorio
            .ExisteAsync(evento.IdEvento, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _documentoRepositorio
            .ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);
        _resumoRepositorio
            .ObterPorDocumentoIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ResumoDocumentoFiscal?>(null));
        ResumoDocumentoFiscal? resumoAdicionado = null;
        EventoConsumido? eventoConsumidoAdicionado = null;
        _resumoRepositorio
            .AdicionarAsync(
                Arg.Do<ResumoDocumentoFiscal>(item => resumoAdicionado = item),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _eventoConsumidoRepositorio
            .AdicionarAsync(
                Arg.Do<EventoConsumido>(item => eventoConsumidoAdicionado = item),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var foiProcessado = await _processador.ProcessarAsync(
            evento,
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(foiProcessado, Is.True);
            Assert.That(resumoAdicionado, Is.Not.Null);
            Assert.That(resumoAdicionado!.DocumentoFiscalId, Is.EqualTo(documento.Id));
            Assert.That(resumoAdicionado.Descricao, Does.Contain("criado"));
            Assert.That(eventoConsumidoAdicionado, Is.Not.Null);
            Assert.That(eventoConsumidoAdicionado!.IdEvento, Is.EqualTo(evento.IdEvento));
            Assert.That(
                eventoConsumidoAdicionado.Consumidor,
                Is.EqualTo(ProcessadorEventosDocumentosFiscais.NomeConsumidor));
        }
        await _unidadeTrabalho.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Processar_QuandoResumoExistir_DeveAtualizaLoSemCriarOutro()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();
        var evento = CriarEvento(documento.Id, AcaoDocumentoFiscal.Atualizado);
        var resumo = new ResumoDocumentoFiscal(
            documento.Id,
            "Descrição anterior",
            FabricaObjetosTeste.Instante.AddMinutes(-1));
        _eventoConsumidoRepositorio
            .ExisteAsync(evento.IdEvento, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _documentoRepositorio
            .ObterPorIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(documento);
        _resumoRepositorio
            .ObterPorDocumentoIdAsync(documento.Id, Arg.Any<CancellationToken>())
            .Returns(resumo);

        var foiProcessado = await _processador.ProcessarAsync(
            evento,
            CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(foiProcessado, Is.True);
            Assert.That(resumo.Descricao, Does.Contain("atualizado"));
            Assert.That(resumo.GeradoEm, Is.EqualTo(FabricaObjetosTeste.Instante));
        }
        await _resumoRepositorio.DidNotReceiveWithAnyArgs()
            .AdicionarAsync(default!, default);
    }

    [Test]
    public void Processar_QuandoDocumentoNaoExistir_DeveLancarNaoEncontrado()
    {
        var documentoId = Guid.NewGuid();
        var evento = CriarEvento(documentoId);
        _eventoConsumidoRepositorio
            .ExisteAsync(evento.IdEvento, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _documentoRepositorio
            .ObterPorIdAsync(documentoId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DocumentoFiscal?>(null));

        var excecao = Assert.ThrowsAsync<DocumentoFiscalNaoEncontradoException>(
            (Func<Task>)(() =>
                _processador.ProcessarAsync(evento, CancellationToken.None)));

        Assert.That(excecao!.Message, Does.Contain(documentoId.ToString()));
    }

    private static DocumentoFiscalProcessadoEvento CriarEvento(
        Guid documentoId,
        AcaoDocumentoFiscal acao = AcaoDocumentoFiscal.Criado) =>
        new(
            Guid.NewGuid(),
            documentoId,
            TipoDocumentoFiscal.NFe,
            acao,
            FabricaObjetosTeste.Instante.AddMinutes(-1));
}
