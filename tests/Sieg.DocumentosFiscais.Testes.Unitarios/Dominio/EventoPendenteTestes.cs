using Sieg.DocumentosFiscais.Dominio.Entidades;
using Sieg.DocumentosFiscais.Testes.Unitarios.Utilitarios;

namespace Sieg.DocumentosFiscais.Testes.Unitarios.Dominio;

[TestFixture]
public sealed class EventoPendenteTestes
{
    [Test]
    public void RegistrarFalha_DeveIncrementarTentativasETruncarErro()
    {
        var evento = new EventoPendente(
            "DocumentoFiscalProcessadoEvento",
            "{}",
            FabricaObjetosTeste.Instante);
        var erroLongo = new string('x', 1_050);
        var proximaTentativa = FabricaObjetosTeste.Instante.AddMinutes(1);

        evento.RegistrarFalha(erroLongo, proximaTentativa);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evento.QuantidadeTentativas, Is.EqualTo(1));
            Assert.That(evento.UltimoErro, Has.Length.EqualTo(1_000));
            Assert.That(evento.ProximaTentativaEm, Is.EqualTo(proximaTentativa));
        }
    }

    [Test]
    public void RegistrarFalha_ComProximaTentativaNoMesmoInstante_DeveLancarExcecao()
    {
        var evento = new EventoPendente(
            "DocumentoFiscalProcessadoEvento",
            "{}",
            FabricaObjetosTeste.Instante);

        var excecao = Assert.Throws<ArgumentException>((Action)(() => evento.RegistrarFalha(
            "Falha temporária",
            FabricaObjetosTeste.Instante)));

        Assert.That(excecao!.ParamName, Is.EqualTo("proximaTentativaEm"));
    }

    [Test]
    public void MarcarComoPublicado_DeveRegistrarDataELimparUltimoErro()
    {
        var evento = new EventoPendente(
            "DocumentoFiscalProcessadoEvento",
            "{}",
            FabricaObjetosTeste.Instante);
        evento.RegistrarFalha("Falha temporária", FabricaObjetosTeste.Instante.AddMinutes(1));
        var publicadoEm = FabricaObjetosTeste.Instante.AddMinutes(2);

        evento.MarcarComoPublicado(publicadoEm);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(evento.PublicadoEm, Is.EqualTo(publicadoEm));
            Assert.That(evento.UltimoErro, Is.Null);
        }
    }
}
