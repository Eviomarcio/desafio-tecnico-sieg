using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sieg.DocumentosFiscais.Aplicacao.Eventos;
using Sieg.DocumentosFiscais.Infraestrutura.Mensageria;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia;

var construtor = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

construtor.Logging.ClearProviders();
construtor.Logging.AddConsole();

construtor.Services.AdicionarPersistencia(construtor.Configuration);
construtor.Services.AdicionarConsumidorMensageria(construtor.Configuration);
construtor.Services.AddSingleton(TimeProvider.System);
construtor.Services.AddScoped<
    IProcessadorEventosDocumentosFiscais,
    ProcessadorEventosDocumentosFiscais>();

var aplicacao = construtor.Build();
await aplicacao.RunAsync();
