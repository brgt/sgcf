using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="EconomiaNegociacao"/> para
/// a tabela <c>sgcf.economia_negociacao</c>.
/// SPEC §3.1, §5.2, §8.1.
///
/// Snapshot imutável: nenhum UPDATE deve ser emitido após a inserção inicial.
/// Os JSONBs de proposta e contrato são armazenados como <c>jsonb</c> para
/// permitir queries parciais no futuro (ex: filtrar por taxa do snapshot).
/// </summary>
internal sealed class EconomiaNegociacaoConfiguration : IEntityTypeConfiguration<EconomiaNegociacao>
{
    public void Configure(EntityTypeBuilder<EconomiaNegociacao> builder)
    {
        builder.ToTable("economia_negociacao");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(e => e.CotacaoId)
            .HasColumnName("cotacao_id")
            .HasColumnType("uuid")
            .IsRequired();
        // 1:1 com Cotacao: apenas uma economia por cotação.
        builder.HasIndex(e => e.CotacaoId).IsUnique();

        builder.Property(e => e.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();
        // 1:1 com Contrato: apenas uma economia por contrato gerado.
        builder.HasIndex(e => e.ContratoId).IsUnique();

        // Snapshots imutáveis em jsonb para auditoria futura (SPEC §5.2).
        builder.Property(e => e.SnapshotPropostaJson)
            .HasColumnName("snapshot_proposta_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.SnapshotContratoJson)
            .HasColumnName("snapshot_contrato_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.CetPropostaAaPercentual)
            .HasColumnName("cet_proposta_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(e => e.CetContratoAaPercentual)
            .HasColumnName("cet_contrato_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(e => e.EconomiaBrlDecimal)
            .HasColumnName("economia_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.EconomiaAjustadaCdiBrlDecimal)
            .HasColumnName("economia_ajustada_cdi_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.DataReferenciaCdi)
            .HasColumnName("data_referencia_cdi")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // FK: economia.cotacao_id → cotacao.id (Restrict — não excluir cotação com economia)
        builder.HasOne<Cotacao>()
            .WithMany()
            .HasForeignKey(e => e.CotacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: economia.contrato_id → contrato.id (Restrict — snapshot é evidência histórica)
        builder.HasOne<Domain.Contratos.Contrato>()
            .WithMany()
            .HasForeignKey(e => e.ContratoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Propriedades computadas (Money wrappers) não são persistidas.
        builder.Ignore(e => e.EconomiaBrl);
        builder.Ignore(e => e.EconomiaAjustadaCdiBrl);
    }
}
