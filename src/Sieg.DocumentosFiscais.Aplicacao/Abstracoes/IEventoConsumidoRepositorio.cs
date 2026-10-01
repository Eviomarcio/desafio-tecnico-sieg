using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IEventoConsumidoRepositorio
{
    Task<bool> ExisteAsync(
        Guid idEvento,
        string consumidor,
        CancellationToken cancellationToken);

    Task AdicionarAsync(
        EventoConsumido evento,
        CancellationToken cancellationToken);
}
