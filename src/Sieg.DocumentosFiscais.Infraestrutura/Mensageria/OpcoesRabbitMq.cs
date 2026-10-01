namespace Sieg.DocumentosFiscais.Infraestrutura.Mensageria;

public sealed class OpcoesRabbitMq
{
    public const string NomeSecao = "RabbitMq";

    public string Servidor { get; init; } = string.Empty;
    public int Porta { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string Senha { get; init; } = string.Empty;
    public string HostVirtual { get; init; } = string.Empty;
    public string NomeExchange { get; init; } = string.Empty;
    public string NomeFila { get; init; } = string.Empty;
    public string ChaveRoteamento { get; init; } = string.Empty;
    public string NomeExchangeRetentativa { get; init; } = string.Empty;
    public int[] IntervalosRetentativaSegundos { get; init; } = [];
    public string NomeExchangeFalhas { get; init; } = string.Empty;
    public string NomeFilaFalhas { get; init; } = string.Empty;
    public string ChaveRoteamentoFalhas { get; init; } = string.Empty;
    public int LimiteMensagensNaoConfirmadas { get; init; }
    public int QuantidadeLoteOutbox { get; init; }
    public int IntervaloPublicacaoSegundos { get; init; }

    public TimeSpan IntervaloPublicacao =>
        TimeSpan.FromSeconds(IntervaloPublicacaoSegundos);
}
