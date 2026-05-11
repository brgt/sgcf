using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Hedge;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class PosicaoSnapshotConfiguration : IEntityTypeConfiguration<PosicaoSnapshot>
{
    public void Configure(EntityTypeBuilder<PosicaoSnapshot> builder)
    {
        builder.ToTable("posicao_snapshot");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(p => p.HedgeId)
            .HasColumnName("hedge_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(p => p.MtmBrlDecimal)
            .HasColumnName("mtm_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(p => p.SpotUtilizadoDecimal)
            .HasColumnName("spot_utilizado")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(p => p.CalculadoEm)
            .HasColumnName("calculado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(p => p.TipoCotacao)
            .HasColumnName("tipo_cotacao")
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.HasIndex(p => new { p.HedgeId, p.CalculadoEm });

        builder.Ignore(p => p.MtmBrl);
        builder.Ignore(p => p.SpotUtilizado);
    }
}
