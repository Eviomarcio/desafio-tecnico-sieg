using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Repositorios;

public sealed class EventoPendenteRepositorio(DocumentosFiscaisDbContext contexto)
    : IEventoPendenteRepositorio
{
    public Task AdicionarAsync(
        EventoPendente evento,
        CancellationToken cancellationToken) =>
        contexto.EventosPendentes.AddAsync(evento, cancellationToken).AsTask();
}
