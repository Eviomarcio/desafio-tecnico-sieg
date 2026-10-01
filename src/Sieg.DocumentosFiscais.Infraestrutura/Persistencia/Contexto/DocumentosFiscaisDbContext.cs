using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
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

    public async Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException excecao)
        {
            throw new DocumentoFiscalConflitoException(
                "O documento foi alterado por outra operação. Atualize os dados e tente novamente.",
                excecao);
        }
        catch (DbUpdateException excecao)
            when (excecao.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            throw new DocumentoFiscalConflitoException(
                "Já existe um documento com o mesmo hash ou a mesma chave fiscal.",
                excecao);
        }
    }
}
