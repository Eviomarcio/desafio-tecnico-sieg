using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Aplicacao.DocumentosFiscais;

public sealed record FiltroDocumentosFiscais(
    TipoDocumentoFiscal? Tipo,
    string? Cnpj,
    string? UnidadeFederativa,
    DateTimeOffset? DataEmissaoInicial,
    DateTimeOffset? DataEmissaoFinal,
    int Pagina = 1,
    int TamanhoPagina = 20)
{
    public int PaginaNormalizada => Math.Max(1, Pagina);

    public int TamanhoPaginaNormalizado => Math.Clamp(TamanhoPagina, 1, 100);
}
