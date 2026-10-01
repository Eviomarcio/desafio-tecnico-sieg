using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IEventoPendenteRepositorio
{
    Task AdicionarAsync(
        EventoPendente evento,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EventoPendente>> ListarProntosParaPublicacaoAsync(
        DateTimeOffset instante,
        int quantidade,
        CancellationToken cancellationToken);
}
