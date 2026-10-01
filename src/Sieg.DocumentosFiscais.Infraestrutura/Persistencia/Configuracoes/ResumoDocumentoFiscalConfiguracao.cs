using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Configuracoes;

public sealed class ResumoDocumentoFiscalConfiguracao
    : IEntityTypeConfiguration<ResumoDocumentoFiscal>
{
    public void Configure(EntityTypeBuilder<ResumoDocumentoFiscal> builder)
    {
        builder.ToTable("resumos_documentos_fiscais");
        builder.HasKey(resumo => resumo.Id);

        builder.Property(resumo => resumo.Id).HasColumnName("id");
        builder.Property(resumo => resumo.DocumentoFiscalId)
            .HasColumnName("documento_fiscal_id");
        builder.Property(resumo => resumo.Descricao)
            .HasColumnName("descricao")
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(resumo => resumo.GeradoEm)
            .HasColumnName("gerado_em")
            .IsRequired();

        builder.HasIndex(resumo => resumo.DocumentoFiscalId)
            .IsUnique()
            .HasDatabaseName("ux_resumos_documentos_fiscais_documento");
        builder.HasOne<DocumentoFiscal>()
            .WithOne()
            .HasForeignKey<ResumoDocumentoFiscal>(resumo => resumo.DocumentoFiscalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
