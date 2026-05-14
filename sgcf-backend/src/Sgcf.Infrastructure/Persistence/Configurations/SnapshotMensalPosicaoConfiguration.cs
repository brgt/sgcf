using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Painel;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class SnapshotMensalPosicaoConfiguration : IEntityTypeConfiguration<SnapshotMensalPosicao>
{
    public void Configure(EntityTypeBuilder<SnapshotMensalPosicao> builder)
    {
        builder.ToTable("snapshot_mensal_posicao");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(s => s.Ano)
            .HasColumnName("ano")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(s => s.Mes)
            .HasColumnName("mes")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(s => s.TotalContratosAtivos)
            .HasColumnName("total_contratos_ativos")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(s => s.SaldoPrincipalBrlDecimal)
            .HasColumnName("saldo_principal_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(s => s.TotalParcelasAbertasBrlDecimal)
            .HasColumnName("total_parcelas_abertas_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(s => s.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Idempotency unique constraint: one snapshot per (ano, mes)
        builder.HasIndex(s => new { s.Ano, s.Mes })
            .IsUnique()
            .HasDatabaseName("UX_snapshot_mensal_posicao_ano_mes");

        builder.Ignore(s => s.SaldoPrincipalBrl);
        builder.Ignore(s => s.TotalParcelasAbertasBrl);
    }
}
