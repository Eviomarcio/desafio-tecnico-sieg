namespace Sieg.DocumentosFiscais.Aplicacao.Excecoes;

public sealed class DocumentoFiscalNaoEncontradoException(Guid id)
    : Exception($"O documento fiscal '{id}' não foi encontrado.");
