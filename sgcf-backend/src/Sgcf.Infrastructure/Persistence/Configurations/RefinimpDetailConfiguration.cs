using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class RefinimpDetailConfiguration : IEntityTypeConfiguration<RefinimpDetail>
{
    public void Configure(EntityTypeBuilder<RefinimpDetail> builder)
    {
        builder.ToTable("refinimp_detail");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(r => r.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(r => r.ContratoId).IsUnique();

        builder.Property(r => r.ContratoMaeId).HasColumnName("contrato_mae_id").HasColumnType("uuid").IsRequired();

        // PercentualRefinanciadoDecimal is the backing field for PercentualRefinanciado (Percentual struct)
        builder.Property(r => r.PercentualRefinanciadoDecimal)
            .HasColumnName("percentual_refinanciado")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        // Money is stored as two separate columns: valor + moeda
        builder.Property(r => r.ValorQuitadoNoRefiValor)
            .HasColumnName("valor_quitado_no_refi_valor")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(r => r.ValorQuitadoNoRefiMoeda)
            .HasColumnName("valor_quitado_no_refi_moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        // Computed properties derived from backing fields must be ignored by EF
        builder.Ignore(r => r.PercentualRefinanciado);
        builder.Ignore(r => r.ValorQuitadoNoRefi);
    }
}
