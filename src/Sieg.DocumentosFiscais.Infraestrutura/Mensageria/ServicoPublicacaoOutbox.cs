using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Mensageria;

public sealed class ServicoPublicacaoOutbox(
    IServiceScopeFactory fabricaEscopos,
    OpcoesRabbitMq opcoes,
    TimeProvider provedorTempo,
    ILogger<ServicoPublicacaoOutbox> logger) : BackgroundService
{
    private static readonly Action<ILogger, Guid, Exception?> RegistrarEventoPublicado =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(1, nameof(RegistrarEventoPublicado)),
            "Evento Outbox {EventoId} publicado no RabbitMQ");

    private static readonly Action<ILogger, Guid, DateTimeOffset, string, Exception?>
        RegistrarFalhaPublicacao =
        LoggerMessage.Define<Guid, DateTimeOffset, string>(
            LogLevel.Warning,
            new EventId(2, nameof(RegistrarFalhaPublicacao)),
            "Falha ao publicar o evento Outbox {EventoId}; tipo {TipoErro}; nova tentativa em {ProximaTentativa}");

    private static readonly Action<ILogger, string, Exception?> RegistrarFalhaCiclo =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3, nameof(RegistrarFalhaCiclo)),
            "Falha do tipo {TipoErro} ao executar o ciclo de publicação do Outbox");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var encontrouEventos = false;

            try
            {
                encontrouEventos = await PublicarLoteAsync(stoppingToken) > 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception excecao)
            {
                RegistrarFalhaCiclo(logger, ObterTipoErro(excecao), null);
            }

            var intervalo = encontrouEventos
                ? TimeSpan.FromMilliseconds(200)
                : opcoes.IntervaloPublicacao;

            await Task.Delay(intervalo, provedorTempo, stoppingToken);
        }
    }

    private async Task<int> PublicarLoteAsync(CancellationToken cancellationToken)
    {
        await using var escopo = fabricaEscopos.CreateAsyncScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IEventoPendenteRepositorio>();
        var publicador = escopo.ServiceProvider.GetRequiredService<IPublicadorEventos>();
        var unidadeTrabalho = escopo.ServiceProvider.GetRequiredService<IUnidadeTrabalho>();
        var agora = provedorTempo.GetUtcNow();
        var eventos = await repositorio.ListarProntosParaPublicacaoAsync(
            agora,
            opcoes.QuantidadeLoteOutbox,
            cancellationToken);

        foreach (var evento in eventos)
        {
            await PublicarEventoAsync(
                evento,
                publicador,
                unidadeTrabalho,
                cancellationToken);
        }

        return eventos.Count;
    }

    private async Task PublicarEventoAsync(
        EventoPendente evento,
        IPublicadorEventos publicador,
        IUnidadeTrabalho unidadeTrabalho,
        CancellationToken cancellationToken)
    {
        try
        {
            await publicador.PublicarAsync(evento, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception excecao)
        {
            var proximaTentativa = CalcularProximaTentativa(evento);
            var tipoErro = ObterTipoErro(excecao);
            evento.RegistrarFalha(tipoErro, proximaTentativa);
            await unidadeTrabalho.SalvarAlteracoesAsync(cancellationToken);
            RegistrarFalhaPublicacao(logger, evento.Id, proximaTentativa, tipoErro, null);
            return;
        }

        evento.MarcarComoPublicado(provedorTempo.GetUtcNow());
        await unidadeTrabalho.SalvarAlteracoesAsync(cancellationToken);
        RegistrarEventoPublicado(logger, evento.Id, null);
    }

    private DateTimeOffset CalcularProximaTentativa(EventoPendente evento)
    {
        var expoente = Math.Min(evento.QuantidadeTentativas, 6);
        var segundos = 5 * Math.Pow(2, expoente);
        return provedorTempo.GetUtcNow().AddSeconds(segundos);
    }

    private static string ObterTipoErro(Exception excecao) =>
        excecao.GetBaseException().GetType().Name;
}
