using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Alertas;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class AlertaVencimentoConfiguration : IEntityTypeConfiguration<AlertaVencimento>
{
    public void Configure(EntityTypeBuilder<AlertaVencimento> builder)
    {
        builder.ToTable("alerta_vencimento");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(a => a.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.TipoAlerta)
            .HasColumnName("tipo_alerta")
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(a => a.DataVencimento)
            .HasColumnName("data_vencimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(a => a.DataAlerta)
            .HasColumnName("data_alerta")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(a => a.ValorDecimal)
            .HasColumnName("valor")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(a => a.Moeda)
            .HasColumnName("moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(a => a.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Idempotency unique constraint: one alert per (contrato, horizonte, vencimento)
        builder.HasIndex(a => new { a.ContratoId, a.TipoAlerta, a.DataVencimento })
            .IsUnique()
            .HasDatabaseName("UX_alerta_vencimento_contrato_tipo_data");

        builder.Ignore(a => a.Valor);
    }
}
