namespace Sieg.DocumentosFiscais.Testes.Unitarios.Utilitarios;

internal sealed class ProvedorTempoFixo(DateTimeOffset instante) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instante;
}
