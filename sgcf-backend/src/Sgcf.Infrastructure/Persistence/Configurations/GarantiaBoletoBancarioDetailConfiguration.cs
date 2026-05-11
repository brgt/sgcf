using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaBoletoBancarioDetailConfiguration
    : IEntityTypeConfiguration<GarantiaBoletoBancarioDetail>
{
    public void Configure(EntityTypeBuilder<GarantiaBoletoBancarioDetail> builder)
    {
        builder.ToTable("garantia_boleto_bancario_detail");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.GarantiaId).HasColumnName("garantia_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(e => e.GarantiaId).IsUnique();

        builder.Property(e => e.BancoEmissor).HasColumnName("banco_emissor").HasColumnType("text").IsRequired();
        builder.Property(e => e.QuantidadeBoletos).HasColumnName("quantidade_boletos").HasColumnType("integer").IsRequired();

        builder.Property(e => e.ValorUnitarioDecimal)
            .HasColumnName("valor_unitario").HasColumnType("numeric(20,6)").IsRequired();

        builder.Property(e => e.DataEmissaoInicial).HasColumnName("data_emissao_inicial").HasColumnType("date").IsRequired();
        builder.Property(e => e.DataVencimentoInicial).HasColumnName("data_vencimento_inicial").HasColumnType("date").IsRequired();
        builder.Property(e => e.DataVencimentoFinal).HasColumnName("data_vencimento_final").HasColumnType("date").IsRequired();
        builder.Property(e => e.Periodicidade).HasColumnName("periodicidade").HasColumnType("text").IsRequired();

        builder.HasOne<Garantia>().WithOne().HasForeignKey<GarantiaBoletoBancarioDetail>(e => e.GarantiaId).IsRequired();

        builder.Ignore(e => e.ValorUnitario);
    }
}
