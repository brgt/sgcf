using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Application.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="CdiSnapshot"/> para a tabela <c>sgcf.cdi_snapshot</c>.
/// SPEC §13 decisão 2 — no MVP o CDI é cadastrado manualmente; futura integração ANBIMA.
/// </summary>
internal sealed class CdiSnapshotConfiguration : IEntityTypeConfiguration<CdiSnapshot>
{
    public void Configure(EntityTypeBuilder<CdiSnapshot> builder)
    {
        builder.ToTable("cdi_snapshot");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(s => s.Data)
            .HasColumnName("data")
            .HasColumnType("date")
            .IsRequired();
        // Unique por data: apenas um snapshot por dia útil.
        builder.HasIndex(s => s.Data).IsUnique();

        builder.Property(s => s.CdiAaPercentual)
            .HasColumnName("cdi_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
