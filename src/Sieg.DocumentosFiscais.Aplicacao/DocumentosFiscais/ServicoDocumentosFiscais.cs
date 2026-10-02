using System.Text.Json;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Dominio.Eventos;

namespace Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;

public sealed class ServicoDocumentosFiscais(
    IDocumentoFiscalRepositorio documentoRepositorio,
    IEventoPendenteRepositorio eventoPendenteRepositorio,
    IProcessadorXmlFiscal processadorXml,
    IUnidadeTrabalho unidadeTrabalho,
    TimeProvider provedorTempo) : IServicoDocumentosFiscais
{
    public async Task<ResultadoProcessamentoDto> ProcessarAsync(
        ReadOnlyMemory<byte> conteudo,
        CancellationToken cancellationToken)
    {
        var documentoProcessado = processadorXml.Processar(conteudo);
        var documentoExistente = await documentoRepositorio.ObterPorHashAsync(
            documentoProcessado.HashConteudo,
            cancellationToken);

        if (documentoExistente is not null)
        {
            return new ResultadoProcessamentoDto(
                MapearDetalhes(documentoExistente),
                FoiCriado: false);
        }

        await ValidarChaveFiscalDisponivelAsync(
            documentoProcessado.Tipo,
            documentoProcessado.ChaveFiscal,
            documentoAtualId: null,
            cancellationToken);

        var agora = provedorTempo.GetUtcNow();
        var documento = new DocumentoFiscal(
            documentoProcessado.Tipo,
            documentoProcessado.ChaveFiscal,
            documentoProcessado.CnpjEmitente,
            documentoProcessado.CnpjDestinatario,
            documentoProcessado.UnidadeFederativa,
            documentoProcessado.DataEmissao,
            documentoProcessado.HashConteudo,
            documentoProcessado.ConteudoXml,
            agora);

        await documentoRepositorio.AdicionarAsync(documento, cancellationToken);
        await AdicionarEventoPendenteAsync(
            documento,
            AcaoDocumentoFiscal.Criado,
            agora,
            cancellationToken);
        await unidadeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return new ResultadoProcessamentoDto(
            MapearDetalhes(documento),
            FoiCriado: true);
    }

    public async Task<PaginaResultado<DocumentoFiscalResumoDto>> ListarAsync(
        FiltroDocumentosFiscais filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var (documentos, total) = await documentoRepositorio.ListarAsync(
            filtro,
            cancellationToken);
        var tamanhoPagina = filtro.TamanhoPaginaNormalizado;

        return new PaginaResultado<DocumentoFiscalResumoDto>(
            documentos.Select(MapearResumo).ToArray(),
            filtro.PaginaNormalizada,
            tamanhoPagina,
            total,
            (int)Math.Ceiling(total / (double)tamanhoPagina));
    }

    public async Task<DocumentoFiscalDetalhesDto> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var documento = await ObterDocumentoAsync(id, cancellationToken);
        return MapearDetalhes(documento);
    }

    public async Task<DocumentoFiscalDetalhesDto> AtualizarAsync(
        Guid id,
        ReadOnlyMemory<byte> conteudo,
        CancellationToken cancellationToken)
    {
        var documento = await ObterDocumentoAsync(id, cancellationToken);
        var documentoProcessado = processadorXml.Processar(conteudo);
        var documentoMesmoHash = await documentoRepositorio.ObterPorHashAsync(
            documentoProcessado.HashConteudo,
            cancellationToken);

        if (documentoMesmoHash is not null && documentoMesmoHash.Id != id)
        {
            throw new DocumentoFiscalConflitoException(
                "O conteúdo informado já pertence a outro documento fiscal.");
        }

        await ValidarChaveFiscalDisponivelAsync(
            documentoProcessado.Tipo,
            documentoProcessado.ChaveFiscal,
            id,
            cancellationToken);

        var agora = provedorTempo.GetUtcNow();
        documento.Atualizar(
            documentoProcessado.Tipo,
            documentoProcessado.ChaveFiscal,
            documentoProcessado.CnpjEmitente,
            documentoProcessado.CnpjDestinatario,
            documentoProcessado.UnidadeFederativa,
            documentoProcessado.DataEmissao,
            documentoProcessado.HashConteudo,
            documentoProcessado.ConteudoXml,
            agora);
        await AdicionarEventoPendenteAsync(
            documento,
            AcaoDocumentoFiscal.Atualizado,
            agora,
            cancellationToken);
        await unidadeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return MapearDetalhes(documento);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken)
    {
        var documento = await ObterDocumentoAsync(id, cancellationToken);
        documentoRepositorio.Excluir(documento);
        await unidadeTrabalho.SalvarAlteracoesAsync(cancellationToken);
    }

    private async Task<DocumentoFiscal> ObterDocumentoAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await documentoRepositorio.ObterPorIdAsync(id, cancellationToken)
        ?? throw new DocumentoFiscalNaoEncontradoException(id);

    private async Task ValidarChaveFiscalDisponivelAsync(
        TipoDocumentoFiscal tipo,
        string? chaveFiscal,
        Guid? documentoAtualId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chaveFiscal))
        {
            return;
        }

        var documentoExistente = await documentoRepositorio.ObterPorChaveFiscalAsync(
            tipo,
            chaveFiscal,
            cancellationToken);

        if (documentoExistente is not null && documentoExistente.Id != documentoAtualId)
        {
            throw new DocumentoFiscalConflitoException(
                "Já existe um documento do mesmo tipo e chave fiscal com conteúdo diferente.");
        }
    }

    private async Task AdicionarEventoPendenteAsync(
        DocumentoFiscal documento,
        AcaoDocumentoFiscal acao,
        DateTimeOffset processadoEm,
        CancellationToken cancellationToken)
    {
        var evento = new DocumentoFiscalProcessadoEvento(
            Guid.NewGuid(),
            documento.Id,
            documento.Tipo,
            acao,
            processadoEm);
        var eventoPendente = new EventoPendente(
            nameof(DocumentoFiscalProcessadoEvento),
            JsonSerializer.Serialize(evento),
            processadoEm);

        await eventoPendenteRepositorio.AdicionarAsync(eventoPendente, cancellationToken);
    }

    private static DocumentoFiscalResumoDto MapearResumo(DocumentoFiscal documento) => new(
        documento.Id,
        documento.Tipo,
        MascararChaveFiscal(documento.ChaveFiscal),
        MascararCnpj(documento.CnpjEmitente),
        MascararCnpj(documento.CnpjDestinatario),
        documento.UnidadeFederativa,
        documento.DataEmissao,
        documento.CriadoEm,
        documento.AtualizadoEm);

    private static DocumentoFiscalDetalhesDto MapearDetalhes(DocumentoFiscal documento) => new(
        documento.Id,
        documento.Tipo,
        MascararChaveFiscal(documento.ChaveFiscal),
        MascararCnpj(documento.CnpjEmitente),
        MascararCnpj(documento.CnpjDestinatario),
        documento.UnidadeFederativa,
        documento.DataEmissao,
        documento.HashConteudo,
        documento.CriadoEm,
        documento.AtualizadoEm);

    private static string? MascararCnpj(string? cnpj) =>
        cnpj is { Length: 14 }
            ? $"{cnpj[..2]}.***.***/{cnpj.Substring(8, 4)}-**"
            : cnpj;

    private static string? MascararChaveFiscal(string? chaveFiscal)
    {
        if (chaveFiscal is not { Length: 44 })
        {
            return chaveFiscal;
        }

        const int inicioCnpj = 6;
        const int tamanhoCnpj = 14;
        var cnpjMascarado = $"{chaveFiscal[inicioCnpj..(inicioCnpj + 2)]}**********" +
                            chaveFiscal[(inicioCnpj + 12)..(inicioCnpj + 14)];

        return chaveFiscal[..inicioCnpj] +
               cnpjMascarado +
               chaveFiscal[(inicioCnpj + tamanhoCnpj)..];
    }
}
