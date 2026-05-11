using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaDuplicatasDetailConfiguration : IEntityTypeConfiguration<GarantiaDuplicatasDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaDuplicatasDetail> builder)
    {
        builder.ToTable("garantia_duplicatas_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.PercentualDescontoDecimal)
            .HasColumnName("percentual_desconto").HasColumnType("numeric(10,6)").IsRequired();

        builder.Property(e => e.VencimentoEscalonadoInicio)
            .HasColumnName("vencimento_escalonado_inicio").HasColumnType("date").IsRequired();
        builder.Property(e => e.VencimentoEscalonadoFim)
            .HasColumnName("vencimento_escalonado_fim").HasColumnType("date").IsRequired();

        builder.Property(e => e.QtdDuplicatasCedidas)
            .HasColumnName("qtd_duplicatas_cedidas").HasColumnType("integer").IsRequired();

        builder.Property(e => e.ValorTotalDuplicatasDecimal)
            .HasColumnName("valor_total_duplicatas").HasColumnType("numeric(20,6)").IsRequired();

        builder.Property(e => e.InstrumentoCessaoData)
            .HasColumnName("instrumento_cessao_data").HasColumnType("date").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaDuplicatasDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.PercentualDesconto);
        builder.Ignore(e => e.ValorTotalDuplicatas);
    }
}
