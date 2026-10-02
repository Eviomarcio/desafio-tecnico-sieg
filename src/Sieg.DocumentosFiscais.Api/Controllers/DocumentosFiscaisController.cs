using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sieg.DocumentosFiscais.Api.Modelos;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Infraestrutura.ProcessamentoXml;

namespace Sieg.DocumentosFiscais.Api.Controllers;

[ApiController]
[Route("api/v1/documentos-fiscais")]
public sealed class DocumentosFiscaisController(IServicoDocumentosFiscais servico) : ControllerBase
{
    private const string NomeRotaObterPorId = "ObterDocumentoFiscalPorId";
    private const long TamanhoMaximoRequisicao =
        ProcessadorXmlFiscal.TamanhoMaximoEmBytes + (64 * 1024);

    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ingestao")]
    [RequestSizeLimit(TamanhoMaximoRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = TamanhoMaximoRequisicao)]
    [ProducesResponseType<DocumentoFiscalDetalhesDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<DocumentoFiscalDetalhesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ProcessarAsync(
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        var conteudo = await LerArquivoAsync(arquivo, cancellationToken);
        var resultado = await servico.ProcessarAsync(conteudo, cancellationToken);

        if (!resultado.FoiCriado)
        {
            Response.Headers.Append("Idempotent-Replay", "true");
            return Ok(resultado.Documento);
        }

        return CreatedAtRoute(
            NomeRotaObterPorId,
            new { id = resultado.Documento.Id },
            resultado.Documento);
    }

    [HttpGet]
    [ProducesResponseType<PaginaResultado<DocumentoFiscalResumoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginaResultado<DocumentoFiscalResumoDto>>> ListarAsync(
        [FromQuery] ListarDocumentosFiscaisRequisicao requisicao,
        CancellationToken cancellationToken)
    {
        var filtro = new FiltroDocumentosFiscais(
            requisicao.Tipo,
            requisicao.Cnpj,
            requisicao.UnidadeFederativa,
            requisicao.DataEmissaoInicial,
            requisicao.DataEmissaoFinal,
            requisicao.Pagina,
            requisicao.TamanhoPagina);

        return Ok(await servico.ListarAsync(filtro, cancellationToken));
    }

    [HttpGet("{id:guid}", Name = NomeRotaObterPorId)]
    [ProducesResponseType<DocumentoFiscalDetalhesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentoFiscalDetalhesDto>> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await servico.ObterPorIdAsync(id, cancellationToken));

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("ingestao")]
    [RequestSizeLimit(TamanhoMaximoRequisicao)]
    [RequestFormLimits(MultipartBodyLengthLimit = TamanhoMaximoRequisicao)]
    [ProducesResponseType<DocumentoFiscalDetalhesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DocumentoFiscalDetalhesDto>> AtualizarAsync(
        Guid id,
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        var conteudo = await LerArquivoAsync(arquivo, cancellationToken);
        return Ok(await servico.AtualizarAsync(id, conteudo, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExcluirAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await servico.ExcluirAsync(id, cancellationToken);
        return NoContent();
    }

    private static async Task<byte[]> LerArquivoAsync(
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo.Length == 0)
        {
            throw new XmlFiscalInvalidoException("O arquivo XML está vazio.");
        }

        if (arquivo.Length > ProcessadorXmlFiscal.TamanhoMaximoEmBytes)
        {
            throw new XmlFiscalInvalidoException("O arquivo XML excede o limite de 5 MB.");
        }

        if (!string.Equals(
                Path.GetExtension(arquivo.FileName),
                ".xml",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new XmlFiscalInvalidoException("Envie um arquivo com extensão .xml.");
        }

        await using var memoria = new MemoryStream((int)arquivo.Length);
        await arquivo.CopyToAsync(memoria, cancellationToken);
        return memoria.ToArray();
    }
}
