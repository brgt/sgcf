using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Hedge;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class InstrumentoHedgeConfiguration : IEntityTypeConfiguration<InstrumentoHedge>
{
    public void Configure(EntityTypeBuilder<InstrumentoHedge> builder)
    {
        builder.ToTable("instrumento_hedge");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(h => h.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.Tipo)
            .HasColumnName("tipo")
            .HasConversion(SgcfConverters.TipoHedge)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(h => h.ContraparteId)
            .HasColumnName("contraparte_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.NotionalDecimal)
            .HasColumnName("notional")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(h => h.MoedaBase)
            .HasColumnName("moeda_base")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(h => h.MoedaQuote)
            .HasColumnName("moeda_quote")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(h => h.DataContratacao)
            .HasColumnName("data_contratacao")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(h => h.DataVencimento)
            .HasColumnName("data_vencimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(h => h.StrikeForward)
            .HasColumnName("strike_forward")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(h => h.StrikePut)
            .HasColumnName("strike_put")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(h => h.StrikeCall)
            .HasColumnName("strike_call")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(h => h.Status)
            .HasColumnName("status")
            .HasConversion(SgcfConverters.StatusHedge)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(h => h.ContratoId).IsUnique();

        builder.Ignore(h => h.Notional);
    }
}
