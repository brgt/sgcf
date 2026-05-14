using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sgcf.Domain.Calendario;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class FeriadoConfiguration : IEntityTypeConfiguration<Feriado>
{
    public void Configure(EntityTypeBuilder<Feriado> builder)
    {
        builder.ToTable("feriado");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(f => f.Data)
            .HasColumnName("data")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(f => f.Tipo)
            .HasColumnName("tipo")
            .HasConversion<byte>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(f => f.Escopo)
            .HasColumnName("escopo")
            .HasConversion<byte>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(f => f.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("varchar(120)")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(f => f.Fonte)
            .HasColumnName("fonte")
            .HasConversion<byte>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(f => f.AnoReferencia)
            .HasColumnName("ano_referencia")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(f => new { f.Data, f.Tipo, f.Escopo }).IsUnique();
        builder.HasIndex(f => new { f.AnoReferencia, f.Escopo });
    }
}
