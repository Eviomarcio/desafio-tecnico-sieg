using System.Xml.Linq;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Abstracoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Analisadores;

public sealed class AnalisadorCTe : AnalisadorXmlFiscalBase, IAnalisadorXmlFiscal
{
    public bool PodeAnalisar(XDocument documento) =>
        documento.Root is not null
        && (NomeEh(documento.Root, "CTe", "cteProc")
            || ObterPrimeiroElemento(documento, "infCte") is not null);

    public DocumentoFiscalProcessado Analisar(
        XDocument documento,
        string xmlNormalizado,
        string hashConteudo)
    {
        var informacoes = ObterPrimeiroElemento(documento, "infCte");
        var chave = RemoverPrefixo(
            ExigirValor(ObterAtributo(informacoes, "Id"), "infCte/@Id", "CTe"),
            "CTe");
        var escopo = informacoes ?? documento.Root!;
        var emitente = ObterPrimeiroElemento(escopo, "emit");
        var destinatario = ObterPrimeiroElemento(escopo, "dest")
            ?? ObterPrimeiroElemento(escopo, "toma4", "toma3", "rem");
        var identificacao = ObterPrimeiroElemento(escopo, "ide");

        return new DocumentoFiscalProcessado(
            TipoDocumentoFiscal.CTe,
            chave,
            NormalizarCnpj(ObterPrimeiroValor(emitente ?? escopo, "CNPJ"), "emit/CNPJ"),
            NormalizarCnpj(ObterPrimeiroValor(destinatario ?? escopo, "CNPJ"), "dest/CNPJ"),
            ObterPrimeiroValor(emitente ?? escopo, "UF"),
            ObterDataOpcional(identificacao ?? escopo, "CTe", "dhEmi", "dEmi"),
            hashConteudo,
            xmlNormalizado);
    }
}
