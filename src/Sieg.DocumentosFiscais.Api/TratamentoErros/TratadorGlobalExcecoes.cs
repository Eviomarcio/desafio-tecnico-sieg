using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;

namespace Sieg.DocumentosFiscais.Api.TratamentoErros;

public sealed class TratadorGlobalExcecoes(
    IProblemDetailsService servicoProblemDetails,
    ILogger<TratadorGlobalExcecoes> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, PathString, Exception?> RegistrarErroNaoTratado =
        LoggerMessage.Define<string, PathString>(
            LogLevel.Error,
            new EventId(1, nameof(RegistrarErroNaoTratado)),
            "Erro não tratado em {Metodo} {Caminho}");

    private static readonly Action<ILogger, int, string, Exception?> RegistrarRequisicaoRejeitada =
        LoggerMessage.Define<int, string>(
            LogLevel.Warning,
            new EventId(2, nameof(RegistrarRequisicaoRejeitada)),
            "Requisição rejeitada com status {Status}: {Motivo}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, titulo, detalhe) = MapearErro(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            RegistrarErroNaoTratado(
                logger,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception);
        }
        else
        {
            RegistrarRequisicaoRejeitada(
                logger,
                status,
                exception.Message,
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
        problema.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

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
                (StatusCodes.Status404NotFound, "Documento fiscal não encontrado", exception.Message),
            DocumentoFiscalConflitoException =>
                (StatusCodes.Status409Conflict, "Conflito no documento fiscal", exception.Message),
            XmlFiscalInvalidoException =>
                (StatusCodes.Status422UnprocessableEntity, "XML fiscal inválido", exception.Message),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Requisição inválida", exception.Message),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "Requisição inválida", exception.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "Erro interno", "Não foi possível concluir a operação.")
        };
}
