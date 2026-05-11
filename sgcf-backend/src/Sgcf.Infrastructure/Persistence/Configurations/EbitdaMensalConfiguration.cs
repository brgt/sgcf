using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Painel;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class EbitdaMensalConfiguration : IEntityTypeConfiguration<EbitdaMensal>
{
    public void Configure(EntityTypeBuilder<EbitdaMensal> builder)
    {
        builder.ToTable("ebitda_mensal");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.Ano)
            .HasColumnName("ano")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.Mes)
            .HasColumnName("mes")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(e => e.ValorBrlDecimal)
            .HasColumnName("valor_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(200)")
            .IsRequired();

        // Unique constraint: only one EBITDA per (ano, mes)
        builder.HasIndex(e => new { e.Ano, e.Mes })
            .IsUnique()
            .HasDatabaseName("UX_ebitda_mensal_ano_mes");

        // ValorBrl is a computed property — EF must not map it
        builder.Ignore(e => e.ValorBrl);
    }
}
