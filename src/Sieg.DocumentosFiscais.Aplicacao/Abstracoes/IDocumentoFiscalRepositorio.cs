using Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IDocumentoFiscalRepositorio
{
    Task<DocumentoFiscal?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<DocumentoFiscal?> ObterPorHashAsync(
        string hashConteudo,
        CancellationToken cancellationToken);

    Task<DocumentoFiscal?> ObterPorChaveFiscalAsync(
        TipoDocumentoFiscal tipo,
        string chaveFiscal,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<DocumentoFiscal> Itens, int Total)> ListarAsync(
        FiltroDocumentosFiscais filtro,
        CancellationToken cancellationToken);

    Task AdicionarAsync(
        DocumentoFiscal documento,
        CancellationToken cancellationToken);

    void Excluir(DocumentoFiscal documento);
}
