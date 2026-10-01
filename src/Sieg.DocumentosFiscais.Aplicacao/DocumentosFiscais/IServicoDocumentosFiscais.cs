namespace Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;

public interface IServicoDocumentosFiscais
{
    Task<ResultadoProcessamentoDto> ProcessarAsync(
        ReadOnlyMemory<byte> conteudo,
        CancellationToken cancellationToken);

    Task<PaginaResultado<DocumentoFiscalResumoDto>> ListarAsync(
        FiltroDocumentosFiscais filtro,
        CancellationToken cancellationToken);

    Task<DocumentoFiscalDetalhesDto> ObterPorIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<DocumentoFiscalDetalhesDto> AtualizarAsync(
        Guid id,
        ReadOnlyMemory<byte> conteudo,
        CancellationToken cancellationToken);

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken);
}
