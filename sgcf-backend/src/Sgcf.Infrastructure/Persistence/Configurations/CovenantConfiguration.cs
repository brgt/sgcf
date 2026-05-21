using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Covenants;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class CovenantConfiguration : IEntityTypeConfiguration<Covenant>
{
    public void Configure(EntityTypeBuilder<Covenant> builder)
    {
        builder.ToTable("covenant");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("text")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(c => c.Tipo)
            .HasColumnName("tipo")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.PeriodicidadeVerificacaoMeses)
            .HasColumnName("periodicidade_verificacao_meses")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(c => c.ProximaVerificacaoEm)
            .HasColumnName("proxima_verificacao_em")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(c => c.UltimaVerificacaoEm)
            .HasColumnName("ultima_verificacao_em")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(c => c.ObservacaoVerificacao)
            .HasColumnName("observacao_verificacao")
            .HasColumnType("text")
            .HasMaxLength(2000)
            .IsRequired(false);

        builder.Property(c => c.LimiteNumerico)
            .HasColumnName("limite_numerico")
            .HasColumnType("numeric(18,6)")
            .IsRequired(false);

        builder.Property(c => c.ValorApurado)
            .HasColumnName("valor_apurado")
            .HasColumnType("numeric(18,6)")
            .IsRequired(false);

        builder.Property(c => c.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(c => c.ContratoId)
            .HasDatabaseName("ix_covenant_contrato_id");

        builder.HasIndex(c => new { c.TenantId, c.Status })
            .HasDatabaseName("ix_covenant_tenant_status");
    }
}
