using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Hedge;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class HistoricoMtmDiarioConfiguration : IEntityTypeConfiguration<HistoricoMtmDiario>
{
    public void Configure(EntityTypeBuilder<HistoricoMtmDiario> builder)
    {
        builder.ToTable("historico_mtm_diario");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(h => h.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.HedgeId)
            .HasColumnName("hedge_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.DataReferencia)
            .HasColumnName("data_referencia")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(h => h.PayoffBrlDecimal)
            .HasColumnName("payoff_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(h => h.SpotUtilizado)
            .HasColumnName("spot_utilizado")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(h => h.TipoCotacao)
            .HasColumnName("tipo_cotacao")
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.Property(h => h.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // FK → instrumento_hedge com cascade delete: ao remover o hedge, remove seus históricos.
        builder.HasOne<InstrumentoHedge>()
            .WithMany()
            .HasForeignKey(h => h.HedgeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chave de negócio: garante idempotência no upsert e unicidade por dia/hedge/tenant.
        builder.HasIndex(h => new { h.TenantId, h.HedgeId, h.DataReferencia })
            .IsUnique()
            .HasDatabaseName("ux_historico_mtm_diario_tenant_hedge_data");

        // PayoffBrl é computado em memória — o EF não deve tentar mapeá-lo para uma coluna.
        builder.Ignore(h => h.PayoffBrl);
    }
}
