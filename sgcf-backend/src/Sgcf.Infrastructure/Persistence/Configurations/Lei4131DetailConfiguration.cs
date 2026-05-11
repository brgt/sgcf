using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class Lei4131DetailConfiguration : IEntityTypeConfiguration<Lei4131Detail>
{
    public void Configure(EntityTypeBuilder<Lei4131Detail> builder)
    {
        builder.ToTable("lei4131_detail");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(l => l.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(l => l.ContratoId).IsUnique();

        builder.Property(l => l.SblcNumero).HasColumnName("sblc_numero").HasColumnType("text").IsRequired(false);
        builder.Property(l => l.SblcBancoEmissor).HasColumnName("sblc_banco_emissor").HasColumnType("text").IsRequired(false);
        builder.Property(l => l.SblcValorUsd).HasColumnName("sblc_valor_usd").HasColumnType("numeric(20,6)").IsRequired(false);
        builder.Property(l => l.TemMarketFlex).HasColumnName("tem_market_flex").HasDefaultValue(false);
        builder.Property(l => l.BreakFundingFeePercentual).HasColumnName("break_funding_fee_percentual").HasColumnType("numeric(10,6)").IsRequired(false);
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
