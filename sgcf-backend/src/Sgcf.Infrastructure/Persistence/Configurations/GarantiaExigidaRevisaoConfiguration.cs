using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="GarantiaExigidaRevisao"/> para a tabela
/// <c>sgcf.garantia_exigida_revisao</c>.
///
/// Tabela criada pela migration S34 (T0.6). Append-only — revisões nunca são removidas.
/// Unicidade da revisão vigente por <c>(tenant_id, limite_banco_id)</c> é enforçada pelo
/// índice único parcial <c>ux_garantia_exigida_revisao_vigente</c> (criado via SQL em Up()).
/// RLS por tenant_id alinhada com o padrão das demais tabelas tenant-scoped.
/// </summary>
internal sealed class GarantiaExigidaRevisaoConfiguration : IEntityTypeConfiguration<GarantiaExigidaRevisao>
{
    public void Configure(EntityTypeBuilder<GarantiaExigidaRevisao> builder)
    {
        builder.ToTable("garantia_exigida_revisao");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.LimiteBancoId)
            .HasColumnName("limite_banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(r => r.VigenciaInicio)
            .HasColumnName("vigencia_inicio")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.VigenciaFim)
            .HasColumnName("vigencia_fim")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(r => r.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.Motivo)
            .HasColumnName("motivo")
            .HasColumnType("varchar(256)")
            .IsRequired(false);

        builder.Property(r => r.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("varchar(1024)")
            .IsRequired(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // FK → limite_banco.id (Cascade: revisões são descartadas com o limite).
        builder.HasOne<Domain.Cotacoes.LimiteBanco>()
            .WithMany()
            .HasForeignKey(r => r.LimiteBancoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Coleção de itens: cascade delete.
        // FK física revisao_id criada pela migration S34 em garantia_exigida_item.
        builder.HasMany(r => r.Itens)
            .WithOne()
            .HasForeignKey(i => i.RevisaoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(GarantiaExigidaRevisao.Itens))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Índice no LimiteBancoId para queries de revisões por limite.
        builder.HasIndex(r => r.LimiteBancoId)
            .HasDatabaseName("ix_garantia_exigida_revisao_limite_banco");

        // TenantId para RLS (política: tenant_id = current_setting('app.tenant_id')::uuid).
        builder.HasIndex(r => r.TenantId)
            .HasDatabaseName("ix_garantia_exigida_revisao_tenant");
    }
}
