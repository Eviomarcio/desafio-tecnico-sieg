using Microsoft.EntityFrameworkCore;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

public sealed class DocumentosFiscaisDbContext(
    DbContextOptions<DocumentosFiscaisDbContext> opcoes)
    : DbContext(opcoes), IUnidadeTrabalho
{
    public DbSet<DocumentoFiscal> DocumentosFiscais => Set<DocumentoFiscal>();
    public DbSet<EventoPendente> EventosPendentes => Set<EventoPendente>();
    public DbSet<EventoConsumido> EventosConsumidos => Set<EventoConsumido>();
    public DbSet<ResumoDocumentoFiscal> ResumosDocumentosFiscais => Set<ResumoDocumentoFiscal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DocumentosFiscaisDbContext).Assembly);

    public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);
}
