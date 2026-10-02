using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Sieg.DocumentosFiscais.Api.TratamentoErros;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml;
using Sieg.DocumentosFiscais.Infraestrutura.Mensageria;

var construtor = WebApplication.CreateBuilder(args);

construtor.Logging.ClearProviders();
construtor.Logging.AddConsole();

construtor.Services
    .AddControllers()
    .AddJsonOptions(opcoes =>
        opcoes.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

construtor.Services.AddProblemDetails();
construtor.Services.AddExceptionHandler<TratadorGlobalExcecoes>();
construtor.Services.AddEndpointsApiExplorer();
construtor.Services.AddSwaggerGen();
construtor.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opcoes.AddFixedWindowLimiter("ingestao", limite =>
    {
        limite.PermitLimit = 30;
        limite.Window = TimeSpan.FromMinutes(1);
        limite.QueueLimit = 0;
        limite.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

construtor.Services.AdicionarPersistencia(construtor.Configuration);
construtor.Services.AdicionarProcessamentoXml();
construtor.Services.AdicionarMensageria(construtor.Configuration);
construtor.Services.AddSingleton(TimeProvider.System);
construtor.Services.AddScoped<IServicoDocumentosFiscais, ServicoDocumentosFiscais>();

var aplicacao = construtor.Build();

if (aplicacao.Configuration.GetValue<bool>("BancoDados:AplicarMigracoesAoIniciar"))
{
    await using var escopo = aplicacao.Services.CreateAsyncScope();
    var contexto = escopo.ServiceProvider.GetRequiredService<DocumentosFiscaisDbContext>();
    await contexto.Database.MigrateAsync();
}

if (aplicacao.Environment.IsDevelopment())
{
    aplicacao.UseSwagger();
    aplicacao.UseSwaggerUI();
}

aplicacao.UseExceptionHandler();
aplicacao.UseStatusCodePages(async contexto =>
{
    await Results.Problem(
            statusCode: contexto.HttpContext.Response.StatusCode,
            title: "A requisição não pôde ser processada.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = contexto.HttpContext.TraceIdentifier,
            })
        .ExecuteAsync(contexto.HttpContext);
});

if (aplicacao.Configuration.GetValue("Seguranca:UsarRedirecionamentoHttps", true))
{
    aplicacao.UseHttpsRedirection();
}

aplicacao.UseRateLimiter();

aplicacao.MapControllers();
aplicacao.MapGet("/saude", () => Results.Ok(new { situacao = "saudavel" }));

aplicacao.Run();

public partial class Program;
