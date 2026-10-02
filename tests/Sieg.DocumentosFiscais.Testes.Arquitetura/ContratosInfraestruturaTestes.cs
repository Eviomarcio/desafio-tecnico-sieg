using System.Reflection;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Testes.Arquitetura;

public sealed class ContratosInfraestruturaTestes
{
    private static readonly Assembly MontagemAplicacao = typeof(IUnidadeTrabalho).Assembly;
    private static readonly Assembly MontagemInfraestrutura = typeof(DocumentosFiscaisDbContext).Assembly;

    [Test]
    public void Infraestrutura_DeveImplementarTodosOsContratosDeAbstracoesDaAplicacao()
    {
        var contratos = MontagemAplicacao.GetTypes()
            .Where(tipo => tipo.IsInterface)
            .Where(tipo => tipo.Namespace == "Sieg.DocumentosFiscais.Aplicacao.Abstracoes")
            .OrderBy(tipo => tipo.Name)
            .ToArray();

        var implementacoes = MontagemInfraestrutura.GetTypes()
            .Where(tipo => tipo.IsClass && !tipo.IsAbstract)
            .ToArray();

        var contratosSemImplementacao = contratos
            .Where(contrato => !implementacoes.Any(contrato.IsAssignableFrom))
            .Select(contrato => contrato.FullName)
            .ToArray();

        Assert.That(
            contratosSemImplementacao,
            Is.Empty,
            "Contratos sem implementação na Infraestrutura.");
    }
}
