using Microsoft.EntityFrameworkCore;
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

    public async Task<IReadOnlyList<EventoPendente>> ListarProntosParaPublicacaoAsync(
        DateTimeOffset instante,
        int quantidade,
        CancellationToken cancellationToken) =>
        await contexto.EventosPendentes
            .Where(evento =>
                evento.PublicadoEm == null &&
                evento.ProximaTentativaEm <= instante)
            .OrderBy(evento => evento.CriadoEm)
            .Take(quantidade)
            .ToListAsync(cancellationToken);
}
