using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sieg.DocumentosFiscais.Infraestrutura.Mensageria;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Testes.Integracao.Infraestrutura;

internal sealed class FabricaApi(string conexaoPostgreSql) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracao) =>
            configuracao.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = conexaoPostgreSql
            }));
        builder.ConfigureServices(servicos =>
        {
            var publicadorOutbox = servicos.SingleOrDefault(descritor =>
                descritor.ServiceType == typeof(IHostedService)
                && descritor.ImplementationType == typeof(ServicoPublicacaoOutbox));

            if (publicadorOutbox is not null)
            {
                servicos.Remove(publicadorOutbox);
            }
        });
    }

    internal async Task RecriarBancoAsync()
    {
        await using var escopo = Services.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<DocumentosFiscaisDbContext>();

        await contexto.Database.EnsureDeletedAsync();
        await contexto.Database.MigrateAsync();
    }

    internal async Task<TResult> ConsultarBancoAsync<TResult>(
        Func<DocumentosFiscaisDbContext, Task<TResult>> consulta)
    {
        await using var escopo = Services.CreateAsyncScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<DocumentosFiscaisDbContext>();
        return await consulta(contexto);
    }
}
