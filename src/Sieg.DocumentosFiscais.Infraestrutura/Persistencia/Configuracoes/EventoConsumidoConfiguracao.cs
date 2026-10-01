using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Configuracoes;

public sealed class EventoConsumidoConfiguracao : IEntityTypeConfiguration<EventoConsumido>
{
    public void Configure(EntityTypeBuilder<EventoConsumido> builder)
    {
        builder.ToTable("eventos_consumidos");
        builder.HasKey(evento => new { evento.IdEvento, evento.Consumidor });

        builder.Property(evento => evento.IdEvento).HasColumnName("id_evento");
        builder.Property(evento => evento.Consumidor)
            .HasColumnName("consumidor")
            .HasMaxLength(200);
        builder.Property(evento => evento.ConsumidoEm)
            .HasColumnName("consumido_em")
            .IsRequired();
    }
}
