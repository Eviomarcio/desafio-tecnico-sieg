using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Dominio.Entidades;

public sealed class DocumentoFiscal
{
    public Guid Id { get; private set; }
    public TipoDocumentoFiscal Tipo { get; private set; }
    public string? ChaveFiscal { get; private set; }
    public string? CnpjEmitente { get; private set; }
    public string? CnpjDestinatario { get; private set; }
    public string? UnidadeFederativa { get; private set; }
    public DateTimeOffset? DataEmissao { get; private set; }
    public string HashConteudo { get; private set; } = string.Empty;
    public string ConteudoXml { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset AtualizadoEm { get; private set; }

    private DocumentoFiscal() { }

    public DocumentoFiscal(
        TipoDocumentoFiscal tipo,
        string? chaveFiscal,
        string? cnpjEmitente,
        string? cnpjDestinatario,
        string? unidadeFederativa,
        DateTimeOffset? dataEmissao,
        string hashConteudo,
        string conteudoXml,
        DateTimeOffset criadoEm)
    {
        Id = Guid.NewGuid();
        CriadoEm = criadoEm;

        AtualizarDados(
            tipo,
            chaveFiscal,
            cnpjEmitente,
            cnpjDestinatario,
            unidadeFederativa,
            dataEmissao,
            hashConteudo,
            conteudoXml,
            criadoEm);
    }

    public void Atualizar(
        TipoDocumentoFiscal tipo,
        string? chaveFiscal,
        string? cnpjEmitente,
        string? cnpjDestinatario,
        string? unidadeFederativa,
        DateTimeOffset? dataEmissao,
        string hashConteudo,
        string conteudoXml,
        DateTimeOffset atualizadoEm)
    {
        if (atualizadoEm < CriadoEm)
        {
            throw new ArgumentException(
                "A data de atualização não pode ser anterior à data de criação.",
                nameof(atualizadoEm));
        }

        AtualizarDados(
            tipo,
            chaveFiscal,
            cnpjEmitente,
            cnpjDestinatario,
            unidadeFederativa,
            dataEmissao,
            hashConteudo,
            conteudoXml,
            atualizadoEm);
    }

    private void AtualizarDados(
        TipoDocumentoFiscal tipo,
        string? chaveFiscal,
        string? cnpjEmitente,
        string? cnpjDestinatario,
        string? unidadeFederativa,
        DateTimeOffset? dataEmissao,
        string hashConteudo,
        string conteudoXml,
        DateTimeOffset atualizadoEm)
    {
        ValidarHash(hashConteudo);
        ArgumentException.ThrowIfNullOrWhiteSpace(conteudoXml);

        Tipo = tipo;
        ChaveFiscal = NormalizarTexto(chaveFiscal);
        CnpjEmitente = NormalizarCnpj(cnpjEmitente, nameof(cnpjEmitente));
        CnpjDestinatario = NormalizarCnpj(cnpjDestinatario, nameof(cnpjDestinatario));
        UnidadeFederativa = NormalizarUnidadeFederativa(unidadeFederativa);
        DataEmissao = dataEmissao;
        HashConteudo = hashConteudo.Trim().ToUpperInvariant();
        ConteudoXml = conteudoXml;
        AtualizadoEm = atualizadoEm;
    }

    private static void ValidarHash(string hashConteudo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashConteudo);

        if (hashConteudo.Trim().Length != 64 || !hashConteudo.Trim().All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "O hash do conteúdo deve ser um SHA-256 hexadecimal com 64 caracteres.",
                nameof(hashConteudo));
        }
    }

    private static string? NormalizarCnpj(string? cnpj, string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
        {
            return null;
        }

        var somenteDigitos = new string(cnpj.Where(char.IsDigit).ToArray());
        if (somenteDigitos.Length != 14)
        {
            throw new ArgumentException("O CNPJ deve possuir 14 dígitos.", nomeParametro);
        }

        return somenteDigitos;
    }

    private static string? NormalizarUnidadeFederativa(string? unidadeFederativa)
    {
        if (string.IsNullOrWhiteSpace(unidadeFederativa))
        {
            return null;
        }

        var valorNormalizado = unidadeFederativa.Trim().ToUpperInvariant();
        if (valorNormalizado.Length != 2 || !valorNormalizado.All(char.IsLetter))
        {
            throw new ArgumentException(
                "A unidade federativa deve possuir duas letras.",
                nameof(unidadeFederativa));
        }

        return valorNormalizado;
    }

    private static string? NormalizarTexto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
