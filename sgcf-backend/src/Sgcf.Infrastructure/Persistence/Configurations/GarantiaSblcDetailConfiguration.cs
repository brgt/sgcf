using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaSblcDetailConfiguration : IEntityTypeConfiguration<GarantiaSblcDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaSblcDetail> builder)
    {
        builder.ToTable("garantia_sblc_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.BancoEmissor).HasColumnName("banco_emissor").HasColumnType("text").IsRequired();
        builder.Property(e => e.PaisEmissor).HasColumnName("pais_emissor").HasColumnType("text").IsRequired();
        builder.Property(e => e.SwiftCode).HasColumnName("swift_code").HasColumnType("text").IsRequired(false);
        builder.Property(e => e.ValidadeDias).HasColumnName("validade_dias").HasColumnType("integer").IsRequired();

        builder.Property(e => e.ComissaoAaDecimal)
            .HasColumnName("comissao_aa").HasColumnType("numeric(10,6)").IsRequired(false);

        builder.Property(e => e.NumeroSblc).HasColumnName("numero_sblc").HasColumnType("text").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaSblcDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.ComissaoAa);
    }
}
