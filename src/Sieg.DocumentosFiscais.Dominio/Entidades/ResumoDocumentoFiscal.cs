namespace Sieg.DocumentosFiscais.Dominio.Entidades;

public sealed class ResumoDocumentoFiscal
{
    public Guid Id { get; private set; }
    public Guid DocumentoFiscalId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public DateTimeOffset GeradoEm { get; private set; }

    private ResumoDocumentoFiscal() { }

    public ResumoDocumentoFiscal(
        Guid documentoFiscalId,
        string descricao,
        DateTimeOffset geradoEm)
    {
        if (documentoFiscalId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do documento fiscal deve ser informado.",
                nameof(documentoFiscalId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);

        Id = Guid.NewGuid();
        DocumentoFiscalId = documentoFiscalId;
        Descricao = descricao.Trim();
        GeradoEm = geradoEm;
    }

    public void Atualizar(string descricao, DateTimeOffset geradoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descricao);

        if (geradoEm < GeradoEm)
        {
            throw new ArgumentException(
                "A nova data de geração não pode ser anterior à atual.",
                nameof(geradoEm));
        }

        Descricao = descricao.Trim();
        GeradoEm = geradoEm;
    }
}
