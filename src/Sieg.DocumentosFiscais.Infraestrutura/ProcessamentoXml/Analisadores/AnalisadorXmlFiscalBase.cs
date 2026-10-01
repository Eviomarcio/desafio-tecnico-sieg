using System.Globalization;
using System.Xml.Linq;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml.Analisadores;

public abstract class AnalisadorXmlFiscalBase
{
    protected static XElement? ObterPrimeiroElemento(
        XContainer container,
        params string[] nomes) =>
        ElementosIncluindoRaiz(container)
            .FirstOrDefault(elemento => NomeEh(elemento, nomes));

    protected static string? ObterPrimeiroValor(
        XContainer container,
        params string[] nomes) =>
        ObterPrimeiroElemento(container, nomes)?.Value.Trim();

    protected static string? ObterAtributo(
        XElement? elemento,
        params string[] nomes) =>
        elemento?.Attributes()
            .FirstOrDefault(atributo => nomes.Any(nome =>
                string.Equals(
                    atributo.Name.LocalName,
                    nome,
                    StringComparison.OrdinalIgnoreCase)))?
            .Value
            .Trim();

    protected static bool NomeEh(XElement elemento, params string[] nomes) =>
        nomes.Any(nome => string.Equals(
            elemento.Name.LocalName,
            nome,
            StringComparison.OrdinalIgnoreCase));

    protected static string ExigirValor(string? valor, string campo, string tipoDocumento)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            return valor;
        }

        throw new XmlFiscalInvalidoException(
            $"O campo obrigatório '{campo}' não foi encontrado no XML de {tipoDocumento}.");
    }

    protected static string? NormalizarCnpj(string? cnpj, string campo)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return null;
        }

        var normalizado = new string(cnpj
            .Where(caractere =>
                !char.IsWhiteSpace(caractere)
                && caractere is not '.' and not '/' and not '-')
            .Select(char.ToUpperInvariant)
            .ToArray());
        var formatoValido = normalizado.Length == 14
            && normalizado[..12].All(EhLetraMaiusculaOuDigito)
            && normalizado[12..].All(EhDigito);

        if (!formatoValido)
        {
            throw new XmlFiscalInvalidoException(
                $"O campo '{campo}' não contém um CNPJ numérico ou alfanumérico válido.");
        }

        return normalizado;
    }

    protected static DateTimeOffset? ObterDataOpcional(
        XContainer container,
        string tipoDocumento,
        params string[] nomes)
    {
        var valor = ObterPrimeiroValor(container, nomes);
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(
                valor,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces
                    | DateTimeStyles.AssumeUniversal
                    | DateTimeStyles.AdjustToUniversal,
                out var data))
        {
            return data;
        }

        throw new XmlFiscalInvalidoException(
            $"A data de emissão '{valor}' do XML de {tipoDocumento} é inválida.");
    }

    protected static string RemoverPrefixo(string valor, string prefixo) =>
        valor.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? valor[prefixo.Length..]
            : valor;

    private static IEnumerable<XElement> ElementosIncluindoRaiz(XContainer container) =>
        container is XDocument { Root: not null } documento
            ? documento.Root.DescendantsAndSelf()
            : container.Descendants();

    private static bool EhLetraMaiusculaOuDigito(char caractere) =>
        caractere is >= 'A' and <= 'Z' || EhDigito(caractere);

    private static bool EhDigito(char caractere) => caractere is >= '0' and <= '9';
}
