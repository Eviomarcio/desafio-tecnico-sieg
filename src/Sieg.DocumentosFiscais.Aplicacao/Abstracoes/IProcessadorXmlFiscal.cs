namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IProcessadorXmlFiscal
{
    DocumentoFiscalProcessado Processar(ReadOnlyMemory<byte> conteudo);
}
