using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Abstracoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml;

public sealed class ProcessadorXmlFiscal(IEnumerable<IAnalisadorXmlFiscal> analisadores)
    : IProcessadorXmlFiscal
{
    public const int TamanhoMaximoEmBytes = 5 * 1024 * 1024;
    public const int ProfundidadeMaxima = 64;

    private readonly IReadOnlyList<IAnalisadorXmlFiscal> _analisadores = analisadores.ToArray();

    public DocumentoFiscalProcessado Processar(ReadOnlyMemory<byte> conteudo)
    {
        ValidarTamanho(conteudo);

        try
        {
            ValidarProfundidade(conteudo);
            var documento = CarregarDocumento(conteudo);
            var analisador = _analisadores.SingleOrDefault(item => item.PodeAnalisar(documento))
                ?? throw new XmlFiscalInvalidoException(
                    "O conteúdo não representa uma NFe, CTe ou NFSe suportada.");
            var xmlNormalizado = NormalizarDocumento(documento);
            var hashConteudo = CalcularHash(xmlNormalizado);

            return analisador.Analisar(documento, xmlNormalizado, hashConteudo);
        }
        catch (XmlFiscalInvalidoException)
        {
            throw;
        }
        catch (InvalidOperationException excecao)
        {
            throw new XmlFiscalInvalidoException(
                "O XML corresponde a mais de um tipo de documento fiscal.",
                excecao);
        }
        catch (XmlException excecao)
        {
            throw new XmlFiscalInvalidoException(
                "O conteúdo enviado não é um XML fiscal válido ou seguro.",
                excecao);
        }
    }

    private static void ValidarTamanho(ReadOnlyMemory<byte> conteudo)
    {
        if (conteudo.IsEmpty)
        {
            throw new XmlFiscalInvalidoException("O arquivo XML está vazio.");
        }

        if (conteudo.Length > TamanhoMaximoEmBytes)
        {
            throw new XmlFiscalInvalidoException("O arquivo XML excede o limite de 5 MB.");
        }
    }

    private static void ValidarProfundidade(ReadOnlyMemory<byte> conteudo)
    {
        using var fluxo = new MemoryStream(conteudo.ToArray(), writable: false);
        using var leitor = XmlReader.Create(fluxo, CriarConfiguracoesSeguras());

        while (leitor.Read())
        {
            if (leitor.Depth > ProfundidadeMaxima)
            {
                throw new XmlFiscalInvalidoException(
                    $"O XML excede a profundidade máxima de {ProfundidadeMaxima} níveis.");
            }
        }
    }

    private static XDocument CarregarDocumento(ReadOnlyMemory<byte> conteudo)
    {
        using var fluxo = new MemoryStream(conteudo.ToArray(), writable: false);
        using var leitor = XmlReader.Create(fluxo, CriarConfiguracoesSeguras());
        var documento = XDocument.Load(leitor, LoadOptions.None);

        return documento.Root is null
            ? throw new XmlFiscalInvalidoException("O XML não possui elemento raiz.")
            : documento;
    }

    private static XmlReaderSettings CriarConfiguracoesSeguras() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = TamanhoMaximoEmBytes,
        MaxCharactersFromEntities = 0,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true
    };

    private static string CalcularHash(string xmlNormalizado) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xmlNormalizado)));

    private static string NormalizarDocumento(XDocument documento)
    {
        documento
            .DescendantNodes()
            .OfType<XText>()
            .Where(texto => string.IsNullOrWhiteSpace(texto.Value))
            .ToArray()
            .Remove();

        return documento.ToString(SaveOptions.DisableFormatting);
    }
}
