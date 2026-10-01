using System.Xml.Linq;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Abstracoes;

public interface IAnalisadorXmlFiscal
{
    bool PodeAnalisar(XDocument documento);

    DocumentoFiscalProcessado Analisar(
        XDocument documento,
        string xmlNormalizado,
        string hashConteudo);
}
