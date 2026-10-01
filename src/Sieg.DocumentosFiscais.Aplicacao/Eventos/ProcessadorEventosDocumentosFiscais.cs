using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Aplicacao.Excecoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Dominio.Eventos;

namespace Sieg.DocumentosFiscais.Aplicacao.Eventos;

public sealed class ProcessadorEventosDocumentosFiscais(
    IDocumentoFiscalRepositorio documentoRepositorio,
    IResumoDocumentoFiscalRepositorio resumoRepositorio,
    IEventoConsumidoRepositorio eventoConsumidoRepositorio,
    IUnidadeTrabalho unidadeTrabalho,
    TimeProvider provedorTempo) : IProcessadorEventosDocumentosFiscais
{
    public const string NomeConsumidor = "gerador-resumo-documento-fiscal";

    public async Task<bool> ProcessarAsync(
        DocumentoFiscalProcessadoEvento evento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);

        if (await eventoConsumidoRepositorio.ExisteAsync(
                evento.IdEvento,
                NomeConsumidor,
                cancellationToken))
        {
            return false;
        }

        var documento = await documentoRepositorio.ObterPorIdAsync(
            evento.DocumentoFiscalId,
            cancellationToken)
            ?? throw new DocumentoFiscalNaoEncontradoException(evento.DocumentoFiscalId);
        var agora = provedorTempo.GetUtcNow();
        var descricao = CriarDescricao(documento, evento);
        var resumo = await resumoRepositorio.ObterPorDocumentoIdAsync(
            documento.Id,
            cancellationToken);

        if (resumo is null)
        {
            await resumoRepositorio.AdicionarAsync(
                new ResumoDocumentoFiscal(documento.Id, descricao, agora),
                cancellationToken);
        }
        else
        {
            resumo.Atualizar(descricao, agora);
        }

        await eventoConsumidoRepositorio.AdicionarAsync(
            new EventoConsumido(evento.IdEvento, NomeConsumidor, agora),
            cancellationToken);
        await unidadeTrabalho.SalvarAlteracoesAsync(cancellationToken);

        return true;
    }

    private static string CriarDescricao(
        DocumentoFiscal documento,
        DocumentoFiscalProcessadoEvento evento)
    {
        var identificacao = documento.ChaveFiscal ?? documento.Id.ToString("D");
        return $"Documento {documento.Tipo} {identificacao} " +
               $"{evento.Acao.ToString().ToLowerInvariant()} em {evento.ProcessadoEm:O}.";
    }
}
