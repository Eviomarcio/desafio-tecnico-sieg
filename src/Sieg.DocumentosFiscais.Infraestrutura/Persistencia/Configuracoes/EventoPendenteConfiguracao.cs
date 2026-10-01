using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Configuracoes;

public sealed class EventoPendenteConfiguracao : IEntityTypeConfiguration<EventoPendente>
{
    public void Configure(EntityTypeBuilder<EventoPendente> builder)
    {
        builder.ToTable("eventos_pendentes");
        builder.HasKey(evento => evento.Id);

        builder.Property(evento => evento.Id).HasColumnName("id");
        builder.Property(evento => evento.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(evento => evento.Conteudo)
            .HasColumnName("conteudo")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(evento => evento.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();
        builder.Property(evento => evento.PublicadoEm)
            .HasColumnName("publicado_em");
        builder.Property(evento => evento.ProximaTentativaEm)
            .HasColumnName("proxima_tentativa_em")
            .IsRequired();
        builder.Property(evento => evento.QuantidadeTentativas)
            .HasColumnName("quantidade_tentativas")
            .IsRequired();
        builder.Property(evento => evento.UltimoErro)
            .HasColumnName("ultimo_erro")
            .HasMaxLength(1_000);

        builder.HasIndex(evento => new { evento.PublicadoEm, evento.ProximaTentativaEm })
            .HasDatabaseName("ix_eventos_pendentes_publicacao");
    }
}
