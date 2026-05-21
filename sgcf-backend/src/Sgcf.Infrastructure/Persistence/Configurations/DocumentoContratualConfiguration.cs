using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Documentos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class DocumentoContratualConfiguration : IEntityTypeConfiguration<DocumentoContratual>
{
    public void Configure(EntityTypeBuilder<DocumentoContratual> builder)
    {
        builder.ToTable("documento_contratual", "sgcf");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(d => d.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(d => d.Tipo)
            .HasColumnName("tipo")
            .HasColumnType("integer")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.Nome)
            .HasColumnName("nome")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.UrlArmazenamento)
            .HasColumnName("url_armazenamento")
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(d => d.DataEmissao)
            .HasColumnName("data_emissao")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(d => d.DataVencimento)
            .HasColumnName("data_vencimento")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(d => d.Observacao)
            .HasColumnName("observacao")
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(d => d.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(d => d.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(d => new { d.TenantId, d.ContratoId })
            .HasDatabaseName("ix_documento_contratual_contrato_id");
    }
}
