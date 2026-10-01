using System.Xml.Linq;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Abstracoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Analisadores;

public sealed class AnalisadorNFe : AnalisadorXmlFiscalBase, IAnalisadorXmlFiscal
{
    public bool PodeAnalisar(XDocument documento) =>
        documento.Root is not null
        && (NomeEh(documento.Root, "NFe", "nfeProc")
            || ObterPrimeiroElemento(documento, "infNFe") is not null);

    public DocumentoFiscalProcessado Analisar(
        XDocument documento,
        string xmlNormalizado,
        string hashConteudo)
    {
        var informacoes = ObterPrimeiroElemento(documento, "infNFe");
        var chave = RemoverPrefixo(
            ExigirValor(ObterAtributo(informacoes, "Id"), "infNFe/@Id", "NFe"),
            "NFe");
        var escopo = informacoes ?? documento.Root!;
        var emitente = ObterPrimeiroElemento(escopo, "emit");
        var destinatario = ObterPrimeiroElemento(escopo, "dest");
        var identificacao = ObterPrimeiroElemento(escopo, "ide");

        return new DocumentoFiscalProcessado(
            TipoDocumentoFiscal.NFe,
            chave,
            NormalizarCnpj(ObterPrimeiroValor(emitente ?? escopo, "CNPJ"), "emit/CNPJ"),
            NormalizarCnpj(ObterPrimeiroValor(destinatario ?? escopo, "CNPJ"), "dest/CNPJ"),
            ObterPrimeiroValor(emitente ?? escopo, "UF"),
            ObterDataOpcional(identificacao ?? escopo, "NFe", "dhEmi", "dEmi"),
            hashConteudo,
            xmlNormalizado);
    }
}
