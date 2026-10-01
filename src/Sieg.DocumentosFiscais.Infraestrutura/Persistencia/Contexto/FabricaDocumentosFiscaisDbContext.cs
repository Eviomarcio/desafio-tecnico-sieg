using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

public sealed class FabricaDocumentosFiscaisDbContext
    : IDesignTimeDbContextFactory<DocumentosFiscaisDbContext>
{
    public DocumentosFiscaisDbContext CreateDbContext(string[] args)
    {
        var construtor = new DbContextOptionsBuilder<DocumentosFiscaisDbContext>();
        construtor.UseNpgsql(
            "Host=localhost;Port=5432;Database=documentos_fiscais;Username=postgres;Password=postgres");

        return new DocumentosFiscaisDbContext(construtor.Options);
    }
}
