using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Bancos;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class BancoConfiguration : IEntityTypeConfiguration<Banco>
{
    public void Configure(EntityTypeBuilder<Banco> builder)
    {
        builder.ToTable("banco_config");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(b => b.CodigoCompe)
            .HasColumnName("codigo_compe")
            .HasColumnType("char(3)")
            .HasMaxLength(3)
            .IsRequired();
        builder.HasIndex(b => b.CodigoCompe).IsUnique();

        builder.Property(b => b.RazaoSocial).HasColumnName("razao_social").HasColumnType("text").IsRequired();
        builder.Property(b => b.Apelido).HasColumnName("apelido").HasColumnType("text").IsRequired();

        builder.Property(b => b.AceitaLiquidacaoTotal)
            .HasColumnName("aceita_liquidacao_total")
            .HasDefaultValue(true);

        builder.Property(b => b.AceitaLiquidacaoParcial)
            .HasColumnName("aceita_liquidacao_parcial")
            .HasDefaultValue(true);

        builder.Property(b => b.ExigeAnuenciaExpressa)
            .HasColumnName("exige_anuencia_expressa")
            .HasDefaultValue(false);

        builder.Property(b => b.ExigeParcelaInteira)
            .HasColumnName("exige_parcela_inteira")
            .HasDefaultValue(false);

        builder.Property(b => b.AceitaRefinimp)
            .HasColumnName("aceita_refinimp")
            .HasDefaultValue(true);

        builder.Property(b => b.AvisoPrevioMinDiasUteis)
            .HasColumnName("aviso_previo_min_dias_uteis")
            .HasColumnType("smallint")
            .HasDefaultValue(0);

        builder.Property(b => b.LimiteCreditoBrlDecimal)
            .HasColumnName("limite_credito_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

        builder.Ignore(b => b.LimiteCreditoBrl);
    }
}
