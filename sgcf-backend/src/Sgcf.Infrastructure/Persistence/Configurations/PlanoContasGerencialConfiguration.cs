using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="PlanoContasGerencial"/> — per-tenant.
///
/// Índice único composto <c>(tenant_id, codigo_gerencial)</c> garante que cada tenant
/// tem no máximo uma conta por código (isolação correta multi-tenant).
///
/// Task −1.10: removido HasData (seed agora é feito pelo provisionador via modelo global);
/// adicionado <c>clonada_de_modelo</c>; corrigido índice único para composto.
/// </summary>
internal sealed class PlanoContasGerencialConfiguration : IEntityTypeConfiguration<PlanoContasGerencial>
{
    public void Configure(EntityTypeBuilder<PlanoContasGerencial> builder)
    {
        builder.ToTable("plano_contas_gerencial", "sgcf");

        builder.HasKey(p => p.Id);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
        builder.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(p => p.CodigoGerencial)
            .HasColumnName("codigo_gerencial")
            .HasColumnType("text")
            .HasMaxLength(20)
            .IsRequired();

        // Índice único composto — garante isolação por tenant (Task −1.10).
        // Substitui o índice anterior que era apenas em codigo_gerencial.
        builder.HasIndex(p => new { p.TenantId, p.CodigoGerencial })
            .IsUnique()
            .HasDatabaseName("ix_plano_contas_gerencial_tenant_codigo");

        builder.Property(p => p.Nome)
            .HasColumnName("nome")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.Natureza)
            .HasColumnName("natureza")
            .HasColumnType("text")
            .HasConversion(SgcfConverters.NaturezaConta)
            .IsRequired();

        builder.Property(p => p.CodigoSapB1)
            .HasColumnName("codigo_sap_b1")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(p => p.Ativo)
            .HasColumnName("ativo")
            .HasDefaultValue(true);

        builder.Property(p => p.ClonadaDeModelo)
            .HasColumnName("clonada_de_modelo")
            .HasColumnType("boolean")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();
    }
}
