namespace Sieg.DocumentosFiscais.Dominio.Entidades;

public sealed class EventoPendente
{
    private const int TamanhoMaximoErro = 1_000;

    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Conteudo { get; private set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; private set; }
    public DateTimeOffset? PublicadoEm { get; private set; }
    public DateTimeOffset ProximaTentativaEm { get; private set; }
    public int QuantidadeTentativas { get; private set; }
    public string? UltimoErro { get; private set; }

    private EventoPendente() { }

    public EventoPendente(string tipo, string conteudo, DateTimeOffset criadoEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipo);
        ArgumentException.ThrowIfNullOrWhiteSpace(conteudo);

        Id = Guid.NewGuid();
        Tipo = tipo.Trim();
        Conteudo = conteudo;
        CriadoEm = criadoEm;
        ProximaTentativaEm = criadoEm;
    }

    public void MarcarComoPublicado(DateTimeOffset publicadoEm)
    {
        if (publicadoEm < CriadoEm)
        {
            throw new ArgumentException(
                "A publicação não pode ser anterior à criação do evento.",
                nameof(publicadoEm));
        }

        PublicadoEm = publicadoEm;
        UltimoErro = null;
    }

    public void RegistrarFalha(string erro, DateTimeOffset proximaTentativaEm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(erro);

        if (proximaTentativaEm <= ProximaTentativaEm)
        {
            throw new ArgumentException(
                "A próxima tentativa deve ser posterior à tentativa atual.",
                nameof(proximaTentativaEm));
        }

        QuantidadeTentativas++;
        UltimoErro = erro.Length <= TamanhoMaximoErro
            ? erro
            : erro[..TamanhoMaximoErro];
        ProximaTentativaEm = proximaTentativaEm;
    }
}
