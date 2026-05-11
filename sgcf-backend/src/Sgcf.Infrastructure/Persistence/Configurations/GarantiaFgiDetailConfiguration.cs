using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaFgiDetailConfiguration : IEntityTypeConfiguration<GarantiaFgiDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaFgiDetail> builder)
    {
        builder.ToTable("garantia_fgi_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.TipoFgi).HasColumnName("tipo_fgi").HasColumnType("text").IsRequired();

        builder.Property(e => e.PercentualCoberturaDecimal)
            .HasColumnName("percentual_cobertura").HasColumnType("numeric(10,6)").IsRequired();

        builder.Property(e => e.TaxaFgiAaDecimal)
            .HasColumnName("taxa_fgi_aa").HasColumnType("numeric(10,6)").IsRequired(false);

        builder.Property(e => e.BancoIntermediario)
            .HasColumnName("banco_intermediario").HasColumnType("text").IsRequired(false);
        builder.Property(e => e.CodigoOperacaoBndes)
            .HasColumnName("codigo_operacao_bndes").HasColumnType("text").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaFgiDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.PercentualCobertura);
        builder.Ignore(e => e.TaxaFgiAa);
    }
}
