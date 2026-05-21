using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Sistema;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="ParametroSistema"/> — per-tenant.
///
/// Cada tenant tem exatamente uma linha com Chave = "DEFAULT".
/// O índice único composto (tenant_id, chave) garante isolação.
/// A seed dos registros é feita pelo provisionamento (Task −1.6), não aqui.
/// </summary>
internal sealed class ParametroSistemaConfiguration : IEntityTypeConfiguration<ParametroSistema>
{
    public void Configure(EntityTypeBuilder<ParametroSistema> builder)
    {
        builder.ToTable("parametro_sistema");

        // PK é o Id (Guid v7) — padrão da entidade base
        builder.HasKey(p => p.Id);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        // Discriminador — valor padrão "DEFAULT".
        // Índice único composto (tenant_id, chave) garante um registro por tenant.
        builder.Property(p => p.Chave)
            .HasColumnName("chave")
            .HasColumnType("varchar(32)")
            .HasMaxLength(32)
            .IsRequired();
        builder.HasIndex(p => new { p.TenantId, p.Chave }).IsUnique();

        // Tetão: mapeado via propriedade interna (backing field)
        // A propriedade pública TetaoMensalCapacidadeBrl (Money?) é calculada — não persiste.
        builder.Property(p => p.TetaoMensalCapacidadeBrlDecimal)
            .HasColumnName("tetao_mensal_capacidade_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        // TetaoMensalCapacidadeBrl é computed (Money wrapper) — não mapear
        builder.Ignore(p => p.TetaoMensalCapacidadeBrl);

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
