namespace Sieg.DocumentosFiscais.Dominio.Entidades;

public sealed class EventoConsumido
{
    public Guid IdEvento { get; private set; }
    public string Consumidor { get; private set; } = string.Empty;
    public DateTimeOffset ConsumidoEm { get; private set; }
    private EventoConsumido() { }

    public EventoConsumido(Guid idEvento, string consumidor, DateTimeOffset consumidoEm)
    {
        if (idEvento == Guid.Empty)
        {
            throw new ArgumentException("O identificador do evento deve ser informado.", nameof(idEvento));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(consumidor);

        IdEvento = idEvento;
        Consumidor = consumidor.Trim();
        ConsumidoEm = consumidoEm;
    }
}
