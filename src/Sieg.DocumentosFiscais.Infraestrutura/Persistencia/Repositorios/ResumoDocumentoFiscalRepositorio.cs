using Microsoft.EntityFrameworkCore;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Contexto;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Repositorios;

public sealed class ResumoDocumentoFiscalRepositorio(DocumentosFiscaisDbContext contexto)
    : IResumoDocumentoFiscalRepositorio
{
    public Task<ResumoDocumentoFiscal?> ObterPorDocumentoIdAsync(
        Guid documentoFiscalId,
        CancellationToken cancellationToken) =>
        contexto.ResumosDocumentosFiscais.SingleOrDefaultAsync(
            resumo => resumo.DocumentoFiscalId == documentoFiscalId,
            cancellationToken);

    public Task AdicionarAsync(
        ResumoDocumentoFiscal resumo,
        CancellationToken cancellationToken) =>
        contexto.ResumosDocumentosFiscais.AddAsync(resumo, cancellationToken).AsTask();
}
