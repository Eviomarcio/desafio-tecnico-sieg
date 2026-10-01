using System.Xml.Linq;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Abstracoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Analisadores;

public sealed class AnalisadorNFSe : AnalisadorXmlFiscalBase, IAnalisadorXmlFiscal
{
    public bool PodeAnalisar(XDocument documento) =>
        documento.Root is not null
        && (NomeEh(
                documento.Root,
                "Nfse",
                "NFSe",
                "CompNfse",
                "GerarNfseResposta",
                "ConsultarNfseResposta")
            || ObterPrimeiroElemento(documento, "InfNfse", "infNFSe") is not null);

    public DocumentoFiscalProcessado Analisar(
        XDocument documento,
        string xmlNormalizado,
        string hashConteudo)
    {
        var informacoes = ObterPrimeiroElemento(documento, "InfNfse", "infNFSe");
        var escopo = informacoes ?? documento.Root!;
        var prestador = ObterPrimeiroElemento(
            escopo,
            "PrestadorServico",
            "Prestador",
            "IdentificacaoPrestador");
        var tomador = ObterPrimeiroElemento(
            escopo,
            "TomadorServico",
            "Tomador",
            "IdentificacaoTomador");
        var chave = ObterAtributo(informacoes, "Id")
            ?? ObterPrimeiroValor(escopo, "CodigoVerificacao", "Numero");

        return new DocumentoFiscalProcessado(
            TipoDocumentoFiscal.NFSe,
            ExigirValor(chave, "InfNfse/@Id, CodigoVerificacao ou Numero", "NFSe"),
            NormalizarCnpj(
                ObterPrimeiroValor(prestador ?? escopo, "Cnpj", "CNPJ"),
                "Prestador/Cnpj"),
            NormalizarCnpj(
                ObterPrimeiroValor(tomador ?? escopo, "Cnpj", "CNPJ"),
                "Tomador/Cnpj"),
            ObterPrimeiroValor(prestador ?? escopo, "Uf", "UF"),
            ObterDataOpcional(escopo, "NFSe", "DataEmissao", "dhEmi", "dEmi"),
            hashConteudo,
            xmlNormalizado);
    }
}
