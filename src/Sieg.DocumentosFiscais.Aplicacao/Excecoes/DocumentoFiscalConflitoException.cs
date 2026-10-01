namespace Sieg.DocumentosFiscais.Aplicacao.Excecoes;

public sealed class DocumentoFiscalConflitoException(
    string mensagem,
    Exception? excecaoInterna = null) : Exception(mensagem, excecaoInterna);
