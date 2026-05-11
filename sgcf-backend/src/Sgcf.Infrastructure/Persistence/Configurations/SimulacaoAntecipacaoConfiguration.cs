using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Antecipacao;
using Sgcf.Domain.Common;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class SimulacaoAntecipacaoConfiguration : IEntityTypeConfiguration<SimulacaoAntecipacao>
{
    public void Configure(EntityTypeBuilder<SimulacaoAntecipacao> builder)
    {
        builder.ToTable("simulacao_antecipacao");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(s => s.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.TipoAntecipacao)
            .HasColumnName("tipo_antecipacao")
            .HasColumnType("smallint")
            .HasConversion(
                v => (short)v,
                v => (TipoAntecipacao)v)
            .IsRequired();

        builder.Property(s => s.DataSimulacao)
            .HasColumnName("data_simulacao")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(s => s.DataEfetivaProposta)
            .HasColumnName("data_efetiva_proposta")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(s => s.ValorPrincipalAQuitarValor)
            .HasColumnName("valor_principal_a_quitar")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(s => s.ValorPrincipalAQuitarMoedaId)
            .HasColumnName("valor_principal_a_quitar_moeda")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.ValorTotalSimuladoBrlValor)
            .HasColumnName("valor_total_simulado_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(s => s.CotacaoAplicada)
            .HasColumnName("cotacao_aplicada")
            .HasColumnType("numeric(12,6)")
            .IsRequired(false);

        builder.Property(s => s.TaxaMercadoAtualAa)
            .HasColumnName("taxa_mercado_atual_aa")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(s => s.PadraoAplicado)
            .HasColumnName("padrao_aplicado")
            .HasColumnType("smallint")
            .HasConversion(
                v => (short)v,
                v => (PadraoAntecipacao)v)
            .IsRequired();

        builder.Property(s => s.ComponentesCustoJson)
            .HasColumnName("componentes_custo")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.EconomiaEstimadaBrlValor)
            .HasColumnName("economia_estimada_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(s => s.ObservacoesBanco)
            .HasColumnName("observacoes_banco")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(s => s.Source)
            .HasColumnName("source")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(s => new { s.ContratoId, s.DataSimulacao })
            .HasDatabaseName("idx_simulacao_contrato")
            .IsDescending(false, true);

        // Computed properties backed by internal backing fields must be ignored
        builder.Ignore(s => s.ValorPrincipalAQuitar);
        builder.Ignore(s => s.ValorTotalSimuladoBrl);
        builder.Ignore(s => s.EconomiaEstimadaBrl);
    }
}
