using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaRecebiveisCartaoDetailConfiguration
    : IEntityTypeConfiguration<GarantiaRecebiveisCartaoDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaRecebiveisCartaoDetail> builder)
    {
        builder.ToTable("garantia_recebiveis_cartao_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.OperadoraCartao).HasColumnName("operadora_cartao").HasColumnType("text").IsRequired();
        builder.Property(e => e.TipoRecebivel).HasColumnName("tipo_recebivel").HasColumnType("text").IsRequired();

        builder.Property(e => e.PercentualFaturamentoComprometidoDecimal)
            .HasColumnName("percentual_faturamento_comprometido").HasColumnType("numeric(10,6)").IsRequired();

        builder.Property(e => e.ValorMedioMensalReferenciaDecimal)
            .HasColumnName("valor_medio_mensal_referencia").HasColumnType("numeric(20,6)").IsRequired(false);

        builder.Property(e => e.PrazoRecebimentoDias)
            .HasColumnName("prazo_recebimento_dias").HasColumnType("integer").IsRequired();

        builder.Property(e => e.TermoCessaoUrl)
            .HasColumnName("termo_cessao_url").HasColumnType("text").IsRequired(false);

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaRecebiveisCartaoDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.PercentualFaturamentoComprometido);
        builder.Ignore(e => e.ValorMedioMensalReferencia);
    }
}
