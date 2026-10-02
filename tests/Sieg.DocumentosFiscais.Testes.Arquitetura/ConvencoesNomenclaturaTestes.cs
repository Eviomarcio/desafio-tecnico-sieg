using System.Reflection;
using Sieg.DocumentosFiscais.Api.Controllers;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Testes.Arquitetura;

public sealed class ConvencoesNomenclaturaTestes
{
    private static readonly Assembly[] Montagens =
    {
        typeof(DocumentoFiscal).Assembly,
        typeof(IServicoDocumentosFiscais).Assembly,
        typeof(DocumentosFiscaisDbContext).Assembly,
        typeof(DocumentosFiscaisController).Assembly,
    };

    [Test]
    public void Interfaces_DevemComecarComI()
    {
        var tiposInvalidos = Montagens
            .SelectMany(montagem => montagem.GetTypes())
            .Where(tipo => tipo.IsInterface)
            .Where(tipo => !tipo.Name.StartsWith('I'))
            .Select(tipo => tipo.FullName)
            .ToArray();

        Assert.That(tiposInvalidos, Is.Empty);
    }

    [TestCase("Sieg.DocumentosFiscais.Api.Controllers", "Controller")]
    [TestCase("Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Repositorios", "Repositorio")]
    [TestCase("Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Configuracoes", "Configuracao")]
    public void ClassesDePapeisConhecidos_DevemUsarOSufixoEsperado(
        string espacoNomes,
        string sufixo)
    {
        var tiposInvalidos = Montagens
            .SelectMany(montagem => montagem.GetTypes())
            .Where(tipo => tipo.IsClass && !tipo.IsAbstract)
            .Where(tipo => !tipo.IsNested)
            .Where(tipo => tipo.Namespace == espacoNomes)
            .Where(tipo => !tipo.Name.EndsWith(sufixo, StringComparison.Ordinal))
            .Select(tipo => tipo.FullName)
            .ToArray();

        Assert.That(tiposInvalidos, Is.Empty);
    }

    [Test]
    public void TiposDaSolucao_DevemPermanecerNoEspacoDeNomesDaPropriaCamada()
    {
        var tiposInvalidos = Montagens
            .SelectMany(montagem => montagem.GetTypes()
                .Where(tipo => tipo.IsPublic || tipo.IsNestedPublic)
                .Where(tipo => tipo.Namespace is not null)
                .Where(tipo => !tipo.Namespace!.StartsWith(
                    montagem.GetName().Name!,
                    StringComparison.Ordinal)))
            .Select(tipo => tipo.FullName)
            .ToArray();

        Assert.That(tiposInvalidos, Is.Empty);
    }
}
