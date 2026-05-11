using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaAvalDetailConfiguration : IEntityTypeConfiguration<GarantiaAvalDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaAvalDetail> builder)
    {
        builder.ToTable("garantia_aval_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.AvalistaTipo).HasColumnName("avalista_tipo").HasColumnType("text").IsRequired();
        builder.Property(e => e.AvalistaNome).HasColumnName("avalista_nome").HasColumnType("text").IsRequired();
        builder.Property(e => e.AvalistaDocumento).HasColumnName("avalista_documento").HasColumnType("text").IsRequired();

        builder.Property(e => e.ValorAvalDecimal)
            .HasColumnName("valor_aval").HasColumnType("numeric(20,6)").IsRequired();

        builder.Property(e => e.VigenciaAte)
            .HasColumnName("vigencia_ate").HasColumnType("date").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaAvalDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.ValorAval);
    }
}
