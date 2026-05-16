using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cambio;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class CotacaoFxConfiguration : IEntityTypeConfiguration<CotacaoFx>
{
    public void Configure(EntityTypeBuilder<CotacaoFx> builder)
    {
        builder.ToTable("cotacao_fx");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(c => c.MoedaBase)
            .HasColumnName("moeda_base")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(c => c.MoedaQuote)
            .HasColumnName("moeda_quote")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(c => c.Momento)
            .HasColumnName("momento")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.Tipo)
            .HasColumnName("tipo")
            .HasConversion(SgcfConverters.TipoCotacao)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.ValorCompraDecimal)
            .HasColumnName("valor_compra")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(c => c.ValorVendaDecimal)
            .HasColumnName("valor_venda")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(c => c.Fonte)
            .HasColumnName("fonte")
            .HasColumnType("text")
            .IsRequired();

        builder.HasIndex(c => new { c.MoedaBase, c.MoedaQuote, c.Momento, c.Tipo }).IsUnique();
        builder.HasIndex(c => new { c.MoedaBase, c.Tipo, c.Momento });

        builder.Ignore(c => c.ValorCompra);
        builder.Ignore(c => c.ValorVenda);
    }
}
