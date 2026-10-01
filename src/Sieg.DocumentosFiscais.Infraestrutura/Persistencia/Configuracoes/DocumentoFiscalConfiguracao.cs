using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sieg.DocumentosFiscais.Dominio.Entidades;

namespace Sieg.DocumentosFiscais.Infraestrutura.Persistencia.Configuracoes;

public sealed class DocumentoFiscalConfiguracao : IEntityTypeConfiguration<DocumentoFiscal>
{
    public void Configure(EntityTypeBuilder<DocumentoFiscal> builder)
    {
        builder.ToTable("documentos_fiscais");
        builder.HasKey(documento => documento.Id);

        builder.Property(documento => documento.Id).HasColumnName("id");
        builder.Property(documento => documento.Tipo)
            .HasColumnName("tipo")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(documento => documento.ChaveFiscal)
            .HasColumnName("chave_fiscal")
            .HasMaxLength(100);
        builder.Property(documento => documento.CnpjEmitente)
            .HasColumnName("cnpj_emitente")
            .HasMaxLength(14);
        builder.Property(documento => documento.CnpjDestinatario)
            .HasColumnName("cnpj_destinatario")
            .HasMaxLength(14);
        builder.Property(documento => documento.UnidadeFederativa)
            .HasColumnName("unidade_federativa")
            .HasMaxLength(2);
        builder.Property(documento => documento.DataEmissao)
            .HasColumnName("data_emissao");
        builder.Property(documento => documento.HashConteudo)
            .HasColumnName("hash_conteudo")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(documento => documento.ConteudoXml)
            .HasColumnName("conteudo_xml")
            .HasColumnType("text")
            .IsRequired();
        builder.Property(documento => documento.CriadoEm)
            .HasColumnName("criado_em")
            .IsRequired();
        builder.Property(documento => documento.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .IsRequired();
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasIndex(documento => documento.HashConteudo)
            .IsUnique()
            .HasDatabaseName("ux_documentos_fiscais_hash_conteudo");
        builder.HasIndex(documento => new { documento.Tipo, documento.ChaveFiscal })
            .IsUnique()
            .HasFilter("chave_fiscal IS NOT NULL")
            .HasDatabaseName("ux_documentos_fiscais_tipo_chave_fiscal");
        builder.HasIndex(documento => documento.DataEmissao)
            .HasDatabaseName("ix_documentos_fiscais_data_emissao");
        builder.HasIndex(documento => documento.CnpjEmitente)
            .HasDatabaseName("ix_documentos_fiscais_cnpj_emitente");
        builder.HasIndex(documento => documento.CnpjDestinatario)
            .HasDatabaseName("ix_documentos_fiscais_cnpj_destinatario");
        builder.HasIndex(documento => documento.UnidadeFederativa)
            .HasDatabaseName("ix_documentos_fiscais_unidade_federativa");
    }
}
