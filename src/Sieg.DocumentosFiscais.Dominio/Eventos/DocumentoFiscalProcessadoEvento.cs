using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Dominio.Eventos;

public sealed record DocumentoFiscalProcessadoEvento(
    Guid IdEvento,
    Guid DocumentoFiscalId,
    TipoDocumentoFiscal TipoDocumento,
    AcaoDocumentoFiscal Acao,
    DateTimeOffset ProcessadoEm);
