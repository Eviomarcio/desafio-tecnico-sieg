using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sieg.DocumentosFiscais.Aplicacao.Abstracoes;

namespace Sieg.DocumentosFiscais.Infraestrutura.Mensageria;

public static class InjecaoDependenciaMensageria
{
    public static IServiceCollection AdicionarMensageria(
        this IServiceCollection servicos,
        IConfiguration configuracao)
    {
        servicos
            .AddOptions<OpcoesRabbitMq>()
            .Bind(configuracao.GetSection(OpcoesRabbitMq.NomeSecao))
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.Servidor),
                "Configure o servidor do RabbitMQ.")
            .Validate(
                opcoes => opcoes.Porta is >= 1 and <= 65_535,
                "Configure uma porta válida para o RabbitMQ.")
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.Usuario),
                "Configure o usuário do RabbitMQ.")
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.Senha),
                "Configure a senha do RabbitMQ.")
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.HostVirtual),
                "Configure o host virtual do RabbitMQ.")
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.NomeExchange),
                "Configure o nome do exchange do RabbitMQ.")
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.NomeFila),
                "Configure o nome da fila do RabbitMQ.")
            .Validate(
                opcoes => !string.IsNullOrWhiteSpace(opcoes.ChaveRoteamento),
                "Configure a chave de roteamento do RabbitMQ.")
            .Validate(
                opcoes => opcoes.QuantidadeLoteOutbox is >= 1 and <= 100,
                "Configure a quantidade do lote Outbox entre 1 e 100.")
            .Validate(
                opcoes => opcoes.IntervaloPublicacaoSegundos >= 1,
                "Configure o intervalo de publicação do Outbox com pelo menos um segundo.")
            .ValidateOnStart();

        servicos.AddSingleton(provedor =>
            provedor.GetRequiredService<IOptions<OpcoesRabbitMq>>().Value);
        servicos.AddSingleton<IPublicadorEventos, PublicadorEventosRabbitMq>();
        servicos.AddHostedService<ServicoPublicacaoOutbox>();

        return servicos;
    }
}
