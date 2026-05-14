using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Alertas;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class AlertaExposicaoBancoConfiguration : IEntityTypeConfiguration<AlertaExposicaoBanco>
{
    public void Configure(EntityTypeBuilder<AlertaExposicaoBanco> builder)
    {
        builder.ToTable("alerta_exposicao_banco");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(a => a.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(a => a.DataAlerta)
            .HasColumnName("data_alerta")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(a => a.ExposicaoBrlDecimal)
            .HasColumnName("exposicao_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(a => a.LimiteBrlDecimal)
            .HasColumnName("limite_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(a => a.PercentualOcupacao)
            .HasColumnName("percentual_ocupacao")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(a => a.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Idempotency unique constraint: one alert per (banco, data)
        builder.HasIndex(a => new { a.BancoId, a.DataAlerta })
            .IsUnique()
            .HasDatabaseName("UX_alerta_exposicao_banco_banco_data");

        builder.Ignore(a => a.ExposicaoBrl);
        builder.Ignore(a => a.LimiteBrl);
    }
}
