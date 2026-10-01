namespace Sieg.DocumentosFiscais.Aplicacao.Excecoes;

public sealed class XmlFiscalInvalidoException(
    string mensagem,
    Exception? excecaoInterna = null) : Exception(mensagem, excecaoInterna);
