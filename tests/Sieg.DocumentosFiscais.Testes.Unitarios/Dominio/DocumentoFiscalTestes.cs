using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Testes.Unitarios.Utilitarios;

namespace Sieg.DocumentosFiscais.Testes.Unitarios.Dominio;

[TestFixture]
public sealed class DocumentoFiscalTestes
{
    [Test]
    public void Criar_DeveNormalizarDadosFiscais()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(documento.CnpjEmitente, Is.EqualTo("12345678000195"));
            Assert.That(documento.CnpjDestinatario, Is.EqualTo("98765432000110"));
            Assert.That(documento.UnidadeFederativa, Is.EqualTo("SP"));
            Assert.That(documento.HashConteudo, Is.EqualTo(FabricaObjetosTeste.HashA));
            Assert.That(documento.CriadoEm, Is.EqualTo(FabricaObjetosTeste.Instante));
        }
    }

    [Test]
    public void Criar_ComHashInvalido_DeveLancarExcecao()
    {
        var excecao = Assert.Throws<ArgumentException>((Action)(() => new DocumentoFiscal(
            TipoDocumentoFiscal.NFe,
            "chave",
            null,
            null,
            null,
            null,
            "hash-invalido",
            "<NFe />",
            FabricaObjetosTeste.Instante)));

        Assert.That(excecao!.ParamName, Is.EqualTo("hashConteudo"));
    }

    [Test]
    public void Criar_ComCnpjAlfanumerico_DeveNormalizarELegitimarFormato()
    {
        var documento = new DocumentoFiscal(
            TipoDocumentoFiscal.NFe,
            "chave",
            "ab.cde.fgh/ijkl-01",
            null,
            "SP",
            null,
            FabricaObjetosTeste.HashA,
            "<NFe />",
            FabricaObjetosTeste.Instante);

        Assert.That(documento.CnpjEmitente, Is.EqualTo("ABCDEFGHIJKL01"));
    }

    [Test]
    public void Atualizar_ComDataAnteriorACriacao_DeveLancarExcecao()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();

        var excecao = Assert.Throws<ArgumentException>((Action)(() => documento.Atualizar(
            TipoDocumentoFiscal.CTe,
            "nova-chave",
            null,
            null,
            "RJ",
            null,
            FabricaObjetosTeste.HashB,
            "<CTe />",
            FabricaObjetosTeste.Instante.AddSeconds(-1))));

        Assert.That(excecao!.ParamName, Is.EqualTo("atualizadoEm"));
    }

    [Test]
    public void Atualizar_ComDadosValidos_DeveSubstituirDadosMutaveis()
    {
        var documento = FabricaObjetosTeste.CriarDocumento();
        var atualizadoEm = FabricaObjetosTeste.Instante.AddHours(1);

        documento.Atualizar(
            TipoDocumentoFiscal.CTe,
            " nova-chave ",
            null,
            null,
            "rj",
            atualizadoEm.AddDays(-1),
            FabricaObjetosTeste.HashB.ToLowerInvariant(),
            "<CTe />",
            atualizadoEm);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(documento.Tipo, Is.EqualTo(TipoDocumentoFiscal.CTe));
            Assert.That(documento.ChaveFiscal, Is.EqualTo("nova-chave"));
            Assert.That(documento.UnidadeFederativa, Is.EqualTo("RJ"));
            Assert.That(documento.HashConteudo, Is.EqualTo(FabricaObjetosTeste.HashB));
            Assert.That(documento.AtualizadoEm, Is.EqualTo(atualizadoEm));
        }
    }
}
