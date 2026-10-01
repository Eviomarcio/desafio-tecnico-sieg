using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public sealed record DocumentoFiscalProcessado(
    TipoDocumentoFiscal Tipo,
    string? ChaveFiscal,
    string? CnpjEmitente,
    string? CnpjDestinatario,
    string? UnidadeFederativa,
    DateTimeOffset? DataEmissao,
    string HashConteudo,
    string ConteudoXml);
