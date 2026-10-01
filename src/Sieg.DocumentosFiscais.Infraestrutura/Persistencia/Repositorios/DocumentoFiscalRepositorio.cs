using Microsoft.EntityFrameworkCore;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Repositorios;

public sealed class DocumentoFiscalRepositorio(DocumentosFiscaisDbContext contexto)
    : IDocumentoFiscalRepositorio
{
    public Task<DocumentoFiscal?> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        contexto.DocumentosFiscais.SingleOrDefaultAsync(
            documento => documento.Id == id,
            cancellationToken);

    public Task<DocumentoFiscal?> ObterPorHashAsync(
        string hashConteudo,
        CancellationToken cancellationToken) =>
        contexto.DocumentosFiscais
            .AsNoTracking()
            .SingleOrDefaultAsync(
                documento => documento.HashConteudo == hashConteudo,
                cancellationToken);

    public Task<DocumentoFiscal?> ObterPorChaveFiscalAsync(
        TipoDocumentoFiscal tipo,
        string chaveFiscal,
        CancellationToken cancellationToken) =>
        contexto.DocumentosFiscais
            .AsNoTracking()
            .SingleOrDefaultAsync(
                documento => documento.Tipo == tipo && documento.ChaveFiscal == chaveFiscal,
                cancellationToken);

    public async Task<(IReadOnlyList<DocumentoFiscal> Itens, int Total)> ListarAsync(
        FiltroDocumentosFiscais filtro,
        CancellationToken cancellationToken)
    {
        var consulta = contexto.DocumentosFiscais.AsNoTracking();

        if (filtro.Tipo.HasValue)
        {
            consulta = consulta.Where(documento => documento.Tipo == filtro.Tipo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Cnpj))
        {
            var cnpj = NormalizarCnpjParaConsulta(filtro.Cnpj);
            consulta = consulta.Where(documento =>
                documento.CnpjEmitente == cnpj || documento.CnpjDestinatario == cnpj);
        }

        if (!string.IsNullOrWhiteSpace(filtro.UnidadeFederativa))
        {
            var unidadeFederativa = filtro.UnidadeFederativa.Trim().ToUpperInvariant();
            consulta = consulta.Where(documento =>
                documento.UnidadeFederativa == unidadeFederativa);
        }

        if (filtro.DataEmissaoInicial.HasValue)
        {
            consulta = consulta.Where(documento =>
                documento.DataEmissao >= filtro.DataEmissaoInicial.Value);
        }

        if (filtro.DataEmissaoFinal.HasValue)
        {
            consulta = consulta.Where(documento =>
                documento.DataEmissao <= filtro.DataEmissaoFinal.Value);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .OrderByDescending(documento => documento.DataEmissao)
            .ThenByDescending(documento => documento.CriadoEm)
            .Skip((filtro.PaginaNormalizada - 1) * filtro.TamanhoPaginaNormalizado)
            .Take(filtro.TamanhoPaginaNormalizado)
            .ToListAsync(cancellationToken);

        return (itens, total);
    }

    public Task AdicionarAsync(
        DocumentoFiscal documento,
        CancellationToken cancellationToken) =>
        contexto.DocumentosFiscais.AddAsync(documento, cancellationToken).AsTask();

    public void Excluir(DocumentoFiscal documento) =>
        contexto.DocumentosFiscais.Remove(documento);

    private static string NormalizarCnpjParaConsulta(string cnpj) =>
        new(cnpj
            .Where(caractere =>
                !char.IsWhiteSpace(caractere)
                && caractere is not '.' and not '/' and not '-')
            .Select(char.ToUpperInvariant)
            .ToArray());
}
