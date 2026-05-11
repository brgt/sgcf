using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class FgiDetailConfiguration : IEntityTypeConfiguration<FgiDetail>
{
    public void Configure(EntityTypeBuilder<FgiDetail> builder)
    {
        builder.ToTable("fgi_detail");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(f => f.ContratoId).HasColumnName("contrato_id").HasColumnType("uuid").IsRequired();
        builder.HasIndex(f => f.ContratoId).IsUnique();

        builder.Property(f => f.NumeroOperacaoFgi).HasColumnName("numero_operacao_fgi").HasColumnType("text").IsRequired(false);

        // Backing fields for computed Percentual? properties — EF must map the backing field directly
        builder.Property(f => f.TaxaFgiAaDecimal)
            .HasColumnName("taxa_fgi_aa")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(f => f.PercentualCobertoBacking)
            .HasColumnName("percentual_coberto")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        // Computed properties derived from backing fields must be ignored by EF
        builder.Ignore(f => f.TaxaFgiAa);
        builder.Ignore(f => f.PercentualCoberto);
    }
}
