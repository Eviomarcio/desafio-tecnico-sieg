using Microsoft.Extensions.DependencyInjection;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Abstracoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Analisadores;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml;

public static class InjecaoDependenciaProcessamentoXml
{
    public static IServiceCollection AdicionarProcessamentoXml(
        this IServiceCollection servicos)
    {
        servicos.AddSingleton<IAnalisadorXmlFiscal, AnalisadorNFe>();
        servicos.AddSingleton<IAnalisadorXmlFiscal, AnalisadorCTe>();
        servicos.AddSingleton<IAnalisadorXmlFiscal, AnalisadorNFSe>();
        servicos.AddSingleton<IProcessadorXmlFiscal, ProcessadorXmlFiscal>();

        return servicos;
    }
}
