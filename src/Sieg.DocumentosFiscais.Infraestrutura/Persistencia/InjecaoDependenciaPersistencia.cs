using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Repositorios;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia;

public static class InjecaoDependenciaPersistencia
{
    public static IServiceCollection AdicionarPersistencia(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        var conexao = configuracao.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException(
                "A string de conexão 'PostgreSql' não foi configurada.");

        servicos.AddDbContext<DocumentosFiscaisDbContext>(opcoes =>
            opcoes.UseNpgsql(conexao));
        servicos.AddScoped<IDocumentoFiscalRepositorio, DocumentoFiscalRepositorio>();
        servicos.AddScoped<IEventoPendenteRepositorio, EventoPendenteRepositorio>();
        servicos.AddScoped<IUnidadeTrabalho>(provedor =>
            provedor.GetRequiredService<DocumentosFiscaisDbContext>());

        return servicos;
    }
}
