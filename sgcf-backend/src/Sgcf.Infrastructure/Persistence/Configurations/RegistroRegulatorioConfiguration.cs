using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Conformidade;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class RegistroRegulatorioConfiguration : IEntityTypeConfiguration<RegistroRegulatorio>
{
    public void Configure(EntityTypeBuilder<RegistroRegulatorio> builder)
    {
        builder.ToTable("registro_regulatorio", "sgcf");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.Tipo)
            .HasColumnName("tipo")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(r => r.NumeroRegistro)
            .HasColumnName("numero_registro")
            .HasColumnType("text")
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(r => r.DataRegistro)
            .HasColumnName("data_registro")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(r => r.DataVencimento)
            .HasColumnName("data_vencimento")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(r => r.Observacao)
            .HasColumnName("observacao")
            .HasColumnType("text")
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(r => r.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Queries filtradas por contrato (listagem por contrato).
        builder.HasIndex(r => new { r.TenantId, r.ContratoId })
            .HasDatabaseName("ix_registro_regulatorio_contrato_id");

        // Suporta queries de monitoramento de pendências cross-contrato.
        builder.HasIndex(r => new { r.TenantId, r.Status })
            .HasDatabaseName("ix_registro_regulatorio_tenant_status");
    }
}
