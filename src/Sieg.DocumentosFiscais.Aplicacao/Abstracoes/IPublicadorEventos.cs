using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IPublicadorEventos
{
    Task PublicarAsync(
        EventoPendente evento,
        CancellationToken cancellationToken);
}
