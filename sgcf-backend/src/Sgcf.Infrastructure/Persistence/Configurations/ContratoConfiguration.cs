using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class ContratoConfiguration : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> builder)
    {
        builder.ToTable("contrato");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(c => c.NumeroExterno)
            .HasColumnName("numero_externo")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.CodigoInterno)
            .HasColumnName("codigo_interno")
            .HasColumnType("text")
            .IsRequired(false);
        builder.HasIndex(c => c.CodigoInterno)
            .IsUnique()
            .HasFilter("codigo_interno IS NOT NULL");

        builder.Property(c => c.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.Modalidade)
            .HasColumnName("modalidade")
            .HasConversion(SgcfConverters.Modalidade)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.Moeda)
            .HasColumnName("moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(c => c.ValorPrincipalDecimal)
            .HasColumnName("valor_principal")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(c => c.DataContratacao)
            .HasColumnName("data_contratacao")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.DataVencimento)
            .HasColumnName("data_vencimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.TaxaAaDecimal)
            .HasColumnName("taxa_aa")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(c => c.BaseCalculo)
            .HasColumnName("base_calculo")
            .HasConversion(SgcfConverters.BaseCalculo)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion(SgcfConverters.StatusContrato)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.ContratoPaiId)
            .HasColumnName("contrato_pai_id")
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(c => c.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.Periodicidade)
            .HasColumnName("periodicidade")
            .HasConversion(new ValueConverter<Periodicidade, short>(v => (short)v, v => (Periodicidade)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(c => c.EstruturaAmortizacao)
            .HasColumnName("estrutura_amortizacao")
            .HasConversion(new ValueConverter<EstruturaAmortizacao, short>(v => (short)v, v => (EstruturaAmortizacao)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(c => c.DataPrimeiroVencimento)
            .HasColumnName("data_primeiro_vencimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(c => c.QuantidadeParcelas)
            .HasColumnName("quantidade_parcelas")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(c => c.AnchorDiaMes)
            .HasColumnName("anchor_dia_mes")
            .HasConversion(new ValueConverter<AnchorDiaMes, short>(v => (short)v, v => (AnchorDiaMes)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(c => c.AnchorDiaFixo)
            .HasColumnName("anchor_dia_fixo")
            .HasColumnType("int")
            .IsRequired(false);

        builder.Property(c => c.PeriodicidadeJuros)
            .HasColumnName("periodicidade_juros")
            .HasConversion(new ValueConverter<Periodicidade?, short?>(
                v => v.HasValue ? (short)v.Value : (short?)null,
                v => v.HasValue ? (Periodicidade)v.Value : (Periodicidade?)null))
            .HasColumnType("smallint")
            .IsRequired(false);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasIndex(c => c.BancoId).HasFilter("deleted_at IS NULL");
        builder.HasIndex(c => c.DataVencimento).HasFilter("deleted_at IS NULL");
        builder.HasIndex(c => c.Status).HasFilter("deleted_at IS NULL");
        builder.HasIndex(c => c.ContratoPaiId).HasFilter("contrato_pai_id IS NOT NULL");

        builder.HasQueryFilter(c => c.DeletedAt == null);

        builder.HasMany(c => c.Parcelas)
            .WithOne()
            .HasForeignKey(p => p.ContratoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Garantias)
            .WithOne()
            .HasForeignKey(g => g.ContratoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Parcelas)
            .HasField("_parcelas")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(c => c.Garantias)
            .HasField("_garantias")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.ValorPrincipal);
        builder.Ignore(c => c.TaxaAa);
    }
}
