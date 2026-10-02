using System.Reflection;
using NetArchTest.Rules;
using Sieg.DocumentosFiscais.Api.Controllers;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Testes.Arquitetura;

public sealed class DependenciasCamadasTestes
{
    private const string PrefixoSolucao = "Sieg.DocumentosFiscais.";
    private const string EspacoNomesAplicacao = PrefixoSolucao + "Aplicacao";
    private const string EspacoNomesInfraestrutura = PrefixoSolucao + "Infraestrutura";
    private const string EspacoNomesApi = PrefixoSolucao + "Api";
    private const string EspacoNomesProcessador = PrefixoSolucao + "Processador";

    private static readonly Assembly MontagemDominio = typeof(DocumentoFiscal).Assembly;
    private static readonly Assembly MontagemAplicacao = typeof(IServicoDocumentosFiscais).Assembly;
    private static readonly Assembly MontagemApi = typeof(DocumentosFiscaisController).Assembly;
    private static readonly string[] ReferenciasEsperadasAplicacao =
    {
        "Sieg.DocumentosFiscais.Dominio",
    };

    [Test]
    public void Dominio_NaoDeveDependerDeOutrasCamadas()
    {
        var resultado = Types.InAssembly(MontagemDominio)
            .ShouldNot()
            .HaveDependencyOnAny(
                EspacoNomesAplicacao,
                EspacoNomesInfraestrutura,
                EspacoNomesApi,
                EspacoNomesProcessador)
            .GetResult();

        VerificarResultado(resultado);
        Assert.That(ObterReferenciasDaSolucao(MontagemDominio), Is.Empty);
    }

    [Test]
    public void Aplicacao_DeveDependerSomenteDoDominioEntreAsCamadasInternas()
    {
        var resultado = Types.InAssembly(MontagemAplicacao)
            .ShouldNot()
            .HaveDependencyOnAny(
                EspacoNomesInfraestrutura,
                EspacoNomesApi,
                EspacoNomesProcessador)
            .GetResult();

        VerificarResultado(resultado);
        Assert.That(
            ObterReferenciasDaSolucao(MontagemAplicacao),
            Is.EqualTo(ReferenciasEsperadasAplicacao));
    }

    [Test]
    public void Api_NaoDeveAcessarDiretamenteOContextoDoEfCore()
    {
        var resultado = Types.InAssembly(MontagemApi)
            .ShouldNot()
            .HaveDependencyOnAny(
                EspacoNomesInfraestrutura + ".Persistencia.Contexto",
                "Microsoft.EntityFrameworkCore")
            .GetResult();

        VerificarResultado(resultado);
    }

    [Test]
    public void Controladores_NaoDevemDependerDiretamenteDaInfraestrutura()
    {
        var resultado = Types.InAssembly(MontagemApi)
            .That()
            .ResideInNamespace(EspacoNomesApi + ".Controllers")
            .ShouldNot()
            .HaveDependencyOn(EspacoNomesInfraestrutura)
            .GetResult();

        VerificarResultado(resultado);
    }

    private static string[] ObterReferenciasDaSolucao(Assembly montagem) =>
        montagem.GetReferencedAssemblies()
            .Select(referencia => referencia.Name)
            .OfType<string>()
            .Where(nome => nome.StartsWith(PrefixoSolucao, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void VerificarResultado(TestResult resultado)
    {
        var tiposInvalidos = resultado.FailingTypes is null
            ? string.Empty
            : string.Join(", ", resultado.FailingTypes.Select(tipo => tipo.FullName));

        Assert.That(
            resultado.IsSuccessful,
            Is.True,
            $"Tipos que violaram a regra: {tiposInvalidos}");
    }
}
