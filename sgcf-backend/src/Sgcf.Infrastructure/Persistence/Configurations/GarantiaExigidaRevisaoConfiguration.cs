using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="GarantiaExigidaRevisao"/> para a tabela
/// <c>sgcf.garantia_exigida_revisao</c>.
///
/// Criada em T0.2 (S34). A tabela física será criada pela migration S34 (T0.6).
/// Até lá, o schema não contém esta tabela e os testes de domínio não dependem do EF.
///
/// TRANSIÇÃO: durante T0.2–T0.5 os testes de integração que dependem do banco
/// serão ignorados ou adaptados. Apenas testes unitários de domínio estão cobertos.
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
        // TODO T0.6: após migration criar a FK física revisao_id em limite_banco_garantia_exigida,
        // mover o HasMany de LimiteBancoConfiguration para cá e ajustar HasForeignKey.
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
