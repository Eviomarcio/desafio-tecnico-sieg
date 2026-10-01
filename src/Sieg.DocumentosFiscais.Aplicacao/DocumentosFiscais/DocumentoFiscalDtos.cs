using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;

public sealed record DocumentoFiscalResumoDto(
    Guid Id,
    TipoDocumentoFiscal Tipo,
    string? ChaveFiscal,
    string? CnpjEmitente,
    string? CnpjDestinatario,
    string? UnidadeFederativa,
    DateTimeOffset? DataEmissao,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm);

public sealed record DocumentoFiscalDetalhesDto(
    Guid Id,
    TipoDocumentoFiscal Tipo,
    string? ChaveFiscal,
    string? CnpjEmitente,
    string? CnpjDestinatario,
    string? UnidadeFederativa,
    DateTimeOffset? DataEmissao,
    string HashConteudo,
    string ConteudoXml,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm);

public sealed record PaginaResultado<T>(
    IReadOnlyList<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);

public sealed record ResultadoProcessamentoDto(
    DocumentoFiscalDetalhesDto Documento,
    bool FoiCriado);
