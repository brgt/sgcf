using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaCdbCativoDetailConfiguration : IEntityTypeConfiguration<GarantiaCdbCativoDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaCdbCativoDetail> builder)
    {
        builder.ToTable("garantia_cdb_cativo_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.BancoCustodia).HasColumnName("banco_custodia").HasColumnType("text").IsRequired();
        builder.Property(e => e.NumeroCdb).HasColumnName("numero_cdb").HasColumnType("text").IsRequired();
        builder.Property(e => e.DataEmissaoCdb).HasColumnName("data_emissao_cdb").HasColumnType("date").IsRequired();
        builder.Property(e => e.DataVencimentoCdb).HasColumnName("data_vencimento_cdb").HasColumnType("date").IsRequired();

        builder.Property(e => e.RendimentoAaDecimal)
            .HasColumnName("rendimento_aa").HasColumnType("numeric(10,6)").IsRequired(false);
        builder.Property(e => e.PercentualCdiDecimal)
            .HasColumnName("percentual_cdi").HasColumnType("numeric(10,6)").IsRequired(false);
        builder.Property(e => e.TaxaIrrfAplicacaoDecimal)
            .HasColumnName("taxa_irrf_aplicacao").HasColumnType("numeric(10,6)").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaCdbCativoDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.RendimentoAa);
        builder.Ignore(e => e.PercentualCdi);
        builder.Ignore(e => e.TaxaIrrfAplicacao);
    }
}
