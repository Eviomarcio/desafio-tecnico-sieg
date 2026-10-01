using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IResumoDocumentoFiscalRepositorio
{
    Task<ResumoDocumentoFiscal?> ObterPorDocumentoIdAsync(
        Guid documentoFiscalId,
        CancellationToken cancellationToken);

    Task AdicionarAsync(
        ResumoDocumentoFiscal resumo,
        CancellationToken cancellationToken);
}
