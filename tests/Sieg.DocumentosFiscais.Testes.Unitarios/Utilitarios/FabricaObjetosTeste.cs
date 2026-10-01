using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Enumeracoes;

namespace Sieg.DocumentosFiscais.Testes.Unitarios.Utilitarios;

internal static class FabricaObjetosTeste
{
    internal static readonly DateTimeOffset Instante =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    internal const string HashA =
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    internal const string HashB =
        "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    internal static DocumentoFiscal CriarDocumento(
        string hash = HashA,
        string chave = "35261012345678000195550010000000011000000010") =>
        new(
            TipoDocumentoFiscal.NFe,
            chave,
            "12.345.678/0001-95",
            "98.765.432/0001-10",
            "sp",
            Instante.AddDays(-1),
            hash,
            "<NFe />",
            Instante);

    internal static DocumentoFiscalProcessado CriarDocumentoProcessado(
        string hash = HashA,
        string chave = "35261012345678000195550010000000011000000010") =>
        new(
            TipoDocumentoFiscal.NFe,
            chave,
            "12345678000195",
            "98765432000110",
            "SP",
            Instante.AddDays(-1),
            hash,
            "<NFe />");
}
