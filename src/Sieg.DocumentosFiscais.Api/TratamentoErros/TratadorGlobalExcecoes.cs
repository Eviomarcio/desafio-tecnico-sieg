using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;

namespace Sieg.DocumentosFiscais.Api.TratamentoErros;

public sealed class TratadorGlobalExcecoes(
    IProblemDetailsService servicoProblemDetails,
    ILogger<TratadorGlobalExcecoes> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, PathString, string, string, Exception?>
        RegistrarErroNaoTratado =
        LoggerMessage.Define<string, PathString, string, string>(
            LogLevel.Error,
            new EventId(1, nameof(RegistrarErroNaoTratado)),
            "Erro não tratado em {Metodo} {Caminho}; tipo {TipoErro}; trace {TraceId}");

    private static readonly Action<ILogger, int, string, string, Exception?>
        RegistrarRequisicaoRejeitada =
        LoggerMessage.Define<int, string, string>(
            LogLevel.Warning,
            new EventId(2, nameof(RegistrarRequisicaoRejeitada)),
            "Requisição rejeitada com status {Status}; tipo {TipoErro}; trace {TraceId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, titulo, detalhe) = MapearErro(exception);
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var tipoErro = exception.GetType().Name;

        if (status >= StatusCodes.Status500InternalServerError)
        {
            RegistrarErroNaoTratado(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                tipoErro,
                traceId,
                null);
        }
        else
        {
            RegistrarRequisicaoRejeitada(
                logger,
                status,
                tipoErro,
                traceId,
                null);
        }

        httpContext.Response.StatusCode = status;
        var problema = new ProblemDetails
        {
            Status = status,
            Title = titulo,
            Detail = detalhe,
            Instance = httpContext.Request.Path
        };
        problema.Extensions["traceId"] = traceId;

        return await servicoProblemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problema
        });
    }

    private static (int Status, string Titulo, string Detalhe) MapearErro(Exception exception) =>
        exception switch
        {
            DocumentoFiscalNaoEncontradoException =>
                (StatusCodes.Status404NotFound, "Documento fiscal não encontrado",
                    "O documento fiscal solicitado não foi encontrado."),
            DocumentoFiscalConflitoException =>
                (StatusCodes.Status409Conflict, "Conflito no documento fiscal",
                    "A operação conflita com um documento fiscal existente."),
            XmlFiscalInvalidoException =>
                (StatusCodes.Status422UnprocessableEntity, "XML fiscal inválido",
                    "O conteúdo enviado não pôde ser processado como documento fiscal."),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Requisição inválida",
                    "A requisição contém dados inválidos."),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "Requisição inválida",
                    "A requisição contém dados inválidos."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Erro interno", "Não foi possível concluir a operação.")
        };
}
