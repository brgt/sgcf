using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class FinimpDetailConfiguration : IEntityTypeConfiguration<FinimpDetail>
{
    public void Configure(EntityTypeBuilder<FinimpDetail> builder)
    {
        builder.ToTable("finimp_detail");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(f => f.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(f => f.ContratoId).IsUnique();

        builder.Property(f => f.RofNumero).HasColumnName("rof_numero").HasColumnType("text").IsRequired(false);
        builder.Property(f => f.RofDataEmissao).HasColumnName("rof_data_emissao").HasColumnType("date").IsRequired(false);
        builder.Property(f => f.ExportadorNome).HasColumnName("exportador_nome").HasColumnType("text").IsRequired(false);
        builder.Property(f => f.ExportadorPais).HasColumnName("exportador_pais").HasColumnType("text").IsRequired(false);
        builder.Property(f => f.ProdutoImportado).HasColumnName("produto_importado").HasColumnType("text").IsRequired(false);
        builder.Property(f => f.FaturaReferencia).HasColumnName("fatura_referencia").HasColumnType("text").IsRequired(false);
        builder.Property(f => f.Incoterm).HasColumnName("incoterm").HasColumnType("text").IsRequired(false);
        builder.Property(f => f.BreakFundingFeePercentual).HasColumnName("break_funding_fee_percentual").HasColumnType("numeric(10,6)").IsRequired(false);
        builder.Property(f => f.TemMarketFlex).HasColumnName("tem_market_flex").HasDefaultValue(false);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
