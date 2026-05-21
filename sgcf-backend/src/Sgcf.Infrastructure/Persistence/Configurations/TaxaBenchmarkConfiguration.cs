using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Benchmarks;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class TaxaBenchmarkConfiguration : IEntityTypeConfiguration<TaxaBenchmark>
{
    public void Configure(EntityTypeBuilder<TaxaBenchmark> builder)
    {
        builder.ToTable("taxa_benchmark");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(t => t.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(t => t.TipoBenchmark)
            .HasColumnName("tipo_benchmark")
            .HasColumnType("text")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.DataReferencia)
            .HasColumnName("data_referencia")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(t => t.TaxaAaDecimal)
            .HasColumnName("taxa_aa_decimal")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Ignore(t => t.TaxaAa);

        builder.Property(t => t.Fonte)
            .HasColumnName("fonte")
            .HasColumnType("text")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(t => new { t.TenantId, t.TipoBenchmark, t.DataReferencia })
            .IsUnique()
            .HasDatabaseName("ux_taxa_benchmark_tenant_tipo_data");
    }
}
