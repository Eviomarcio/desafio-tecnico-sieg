using System.Text;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Analisadores;

namespace Sieg.DocumentosFiscais.Testes.Unitarios.ProcessamentoXml;

[TestFixture]
public sealed class ProcessadorXmlFiscalTestes
{
    private ProcessadorXmlFiscal _processador = null!;

    [SetUp]
    public void Preparar()
    {
        _processador = new ProcessadorXmlFiscal(
        [
            new AnalisadorNFe(),
            new AnalisadorCTe(),
            new AnalisadorNFSe()
        ]);
    }

    [Test]
    public void Processar_NFeValida_DeveExtrairDadosFiscais()
    {
        const string xml = """
            <nfeProc xmlns="http://www.portalfiscal.inf.br/nfe">
              <NFe>
                <infNFe Id="NFe35261012345678000195550010000000011000000010">
                  <ide><dhEmi>2026-10-01T09:00:00-03:00</dhEmi></ide>
                  <emit><CNPJ>AB.CDE.FGH/IJKL-01</CNPJ><UF>SP</UF></emit>
                  <dest><CNPJ>98.765.432/0001-10</CNPJ></dest>
                </infNFe>
              </NFe>
            </nfeProc>
            """;

        var resultado = _processador.Processar(Encoding.UTF8.GetBytes(xml));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(resultado.Tipo, Is.EqualTo(TipoDocumentoFiscal.NFe));
            Assert.That(
                resultado.ChaveFiscal,
                Is.EqualTo("35261012345678000195550010000000011000000010"));
            Assert.That(resultado.CnpjEmitente, Is.EqualTo("ABCDEFGHIJKL01"));
            Assert.That(resultado.CnpjDestinatario, Is.EqualTo("98765432000110"));
            Assert.That(resultado.UnidadeFederativa, Is.EqualTo("SP"));
            Assert.That(resultado.DataEmissao, Is.EqualTo(new DateTimeOffset(
                2026,
                10,
                1,
                12,
                0,
                0,
                TimeSpan.Zero)));
            Assert.That(resultado.HashConteudo, Has.Length.EqualTo(64));
        }
    }

    [TestCase("<CTe><infCte Id=\"CTe123\"><emit><CNPJ>12345678000195</CNPJ><UF>PR</UF></emit></infCte></CTe>", TipoDocumentoFiscal.CTe)]
    [TestCase("<CompNfse><InfNfse Id=\"NFSE-1\"><PrestadorServico><Cnpj>12345678000195</Cnpj><Uf>SC</Uf></PrestadorServico></InfNfse></CompNfse>", TipoDocumentoFiscal.NFSe)]
    public void Processar_OutrosTiposSuportados_DeveIdentificarTipo(
        string xml,
        TipoDocumentoFiscal tipoEsperado)
    {
        var resultado = _processador.Processar(Encoding.UTF8.GetBytes(xml));

        Assert.That(resultado.Tipo, Is.EqualTo(tipoEsperado));
    }

    [Test]
    public void Processar_XmlComApenasFormatacaoDiferente_DeveGerarMesmoHash()
    {
        const string compacto =
            "<NFe><infNFe Id=\"NFe1\"><emit><CNPJ>12345678000195</CNPJ></emit></infNFe></NFe>";
        const string identado = """
            <NFe>
              <infNFe Id="NFe1">
                <emit>
                  <CNPJ>12345678000195</CNPJ>
                </emit>
              </infNFe>
            </NFe>
            """;

        var primeiro = _processador.Processar(Encoding.UTF8.GetBytes(compacto));
        var segundo = _processador.Processar(Encoding.UTF8.GetBytes(identado));

        Assert.That(segundo.HashConteudo, Is.EqualTo(primeiro.HashConteudo));
    }

    [Test]
    public void Processar_XmlComDtd_DeveRejeitarConteudoInseguro()
    {
        const string xml = """
            <!DOCTYPE NFe [<!ENTITY arquivo SYSTEM "file:///etc/passwd">]>
            <NFe><infNFe Id="NFe1"><emit><CNPJ>&arquivo;</CNPJ></emit></infNFe></NFe>
            """;

        var excecao = Assert.Throws<XmlFiscalInvalidoException>(
            (Action)(() =>
                _processador.Processar(Encoding.UTF8.GetBytes(xml))));

        Assert.That(excecao!.Message, Does.Contain("válido ou seguro"));
    }

    [Test]
    public void Processar_XmlNaoFiscal_DeveRejeitarTipoNaoSuportado()
    {
        const string xml = "<Pedido><Numero>1</Numero></Pedido>";

        var excecao = Assert.Throws<XmlFiscalInvalidoException>(
            (Action)(() =>
                _processador.Processar(Encoding.UTF8.GetBytes(xml))));

        Assert.That(excecao!.Message, Does.Contain("não representa uma NFe, CTe ou NFSe"));
    }

    [Test]
    public void Processar_ConteudoAcimaDoLimite_DeveRejeitarAntesDaLeitura()
    {
        var conteudo = new byte[ProcessadorXmlFiscal.TamanhoMaximoEmBytes + 1];

        var excecao = Assert.Throws<XmlFiscalInvalidoException>(
            (Action)(() => _processador.Processar(conteudo)));

        Assert.That(excecao!.Message, Does.Contain("excede o limite de 5 MB"));
    }
}
