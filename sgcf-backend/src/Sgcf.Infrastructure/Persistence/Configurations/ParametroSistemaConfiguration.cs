using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Sistema;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="ParametroSistema"/>.
///
/// Design singleton: tabela contém exatamente uma linha.
/// A chave discriminadora <c>Chave = "GLOBAL"</c> é única e funciona como PK natural.
/// A seed da linha padrão é feita pela migration S10 — não aqui, para evitar
/// conflito com ambientes de teste que não rodam todas as migrations.
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

        // Discriminador singleton — valor sempre "GLOBAL", índice único garante a invariante
        builder.Property(p => p.Chave)
            .HasColumnName("chave")
            .HasColumnType("varchar(32)")
            .HasMaxLength(32)
            .IsRequired();
        builder.HasIndex(p => p.Chave).IsUnique();

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
