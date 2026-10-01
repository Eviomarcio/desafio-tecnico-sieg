namespace Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

public interface IUnidadeTrabalho
{
    Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken);
}
