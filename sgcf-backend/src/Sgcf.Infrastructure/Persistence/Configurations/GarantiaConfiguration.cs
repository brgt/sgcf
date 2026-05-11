using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class GarantiaConfiguration : IEntityTypeConfiguration<Garantia>
{
    public void Configure(EntityTypeBuilder<Garantia> builder)
    {
        builder.ToTable("garantia");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(g => g.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(g => g.Tipo)
            .HasColumnName("tipo")
            .HasConversion(SgcfConverters.TipoGarantia)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(g => g.ValorBrlDecimal)
            .HasColumnName("valor_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(g => g.PercentualPrincipalDecimal)
            .HasColumnName("percentual_principal")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(g => g.DataConstituicao)
            .HasColumnName("data_constituicao")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(g => g.DataLiberacaoPrevista)
            .HasColumnName("data_liberacao_prevista")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(g => g.DataLiberacaoEfetiva)
            .HasColumnName("data_liberacao_efetiva")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(g => g.Status)
            .HasColumnName("status")
            .HasConversion(SgcfConverters.StatusGarantia)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(g => g.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(g => g.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(g => g.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(g => g.ContratoId);
        builder.HasIndex(g => new { g.ContratoId, g.Status });

        // Computed properties must be ignored by EF
        builder.Ignore(g => g.ValorBrl);
        builder.Ignore(g => g.PercentualPrincipal);
    }
}
