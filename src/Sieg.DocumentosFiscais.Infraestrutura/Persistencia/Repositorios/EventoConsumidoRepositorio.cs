using Microsoft.EntityFrameworkCore;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Repositorios;

public sealed class EventoConsumidoRepositorio(DocumentosFiscaisDbContext contexto)
    : IEventoConsumidoRepositorio
{
    public Task<bool> ExisteAsync(
        Guid idEvento,
        string consumidor,
        CancellationToken cancellationToken) =>
        contexto.EventosConsumidos.AnyAsync(
            evento => evento.IdEvento == idEvento && evento.Consumidor == consumidor,
            cancellationToken);

    public Task AdicionarAsync(
        EventoConsumido evento,
        CancellationToken cancellationToken) =>
        contexto.EventosConsumidos.AddAsync(evento, cancellationToken).AsTask();
}
