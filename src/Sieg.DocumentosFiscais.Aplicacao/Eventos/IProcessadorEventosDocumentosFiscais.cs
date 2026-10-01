using Sieg.DocumentosFiscais.Dominio.Eventos;

namespace Sieg.DocumentosFiscais.Aplicacao.Eventos;

public interface IProcessadorEventosDocumentosFiscais
{
    Task<bool> ProcessarAsync(
        DocumentoFiscalProcessadoEvento evento,
        CancellationToken cancellationToken);
}
