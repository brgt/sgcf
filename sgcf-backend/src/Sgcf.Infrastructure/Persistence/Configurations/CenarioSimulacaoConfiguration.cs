using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core do agregado raiz <see cref="CenarioSimulacao"/> para
/// a tabela <c>sgcf.cenario_simulacao</c>.
///
/// Decisões de design:
/// - Soft delete via query filter (DeletedAt IS NULL). GetById também filtra.
/// - Coleção _simulacoes via PropertyAccessMode.Field (backing field privado).
/// - Cascade Delete nas simulações filhas: se cenário for hard-deletado (raro),
///   filhas são removidas automaticamente. Soft delete não aciona cascade.
/// - Status como smallint: mais eficiente que text para enum com 3 valores.
/// </summary>
internal sealed class CenarioSimulacaoConfiguration : IEntityTypeConfiguration<CenarioSimulacao>
{
    public void Configure(EntityTypeBuilder<CenarioSimulacao> builder)
    {
        builder.ToTable("cenario_simulacao");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.Nome)
            .HasColumnName("nome")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.Descricao)
            .HasColumnName("descricao")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.AnoBase)
            .HasColumnName("ano_base")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<short>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(c => c.CriadoPor)
            .HasColumnName("criado_por")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Índices para os filtros mais comuns do ListAsync.
        builder.HasIndex(c => new { c.Status, c.CriadoPor })
            .HasFilter("deleted_at IS NULL");
        builder.HasIndex(c => c.AnoBase)
            .HasFilter("deleted_at IS NULL");

        // Soft delete: todas as queries ignoram registros com DeletedAt != null.
        // Para acessar cenários deletados é necessário IgnoreQueryFilters().
        builder.HasQueryFilter(c => c.DeletedAt == null);

        // Coleção de simulações filhas — backing field _simulacoes (privado).
        builder.HasMany(c => c.Simulacoes)
            .WithOne()
            .HasForeignKey(s => s.CenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(CenarioSimulacao.Simulacoes))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
