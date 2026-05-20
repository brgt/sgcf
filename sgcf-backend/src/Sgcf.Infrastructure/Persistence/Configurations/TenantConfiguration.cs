using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenant");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasColumnType("text")
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique().HasDatabaseName("ix_tenant_slug_unique");

        builder.Property(t => t.Nome)
            .HasColumnName("nome")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.CnpjMascarado)
            .HasColumnName("cnpj_mascarado")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<byte>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(t => t.Plano)
            .HasColumnName("plano")
            .HasConversion<byte>()
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(t => t.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(t => t.SuspensoEm)
            .HasColumnName("suspenso_em")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(t => t.ArquivadoEm)
            .HasColumnName("arquivado_em")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Índice parcial: exclui arquivados das buscas de tenants ativos/suspensos (performance).
        builder.HasIndex(t => t.Status)
            .HasFilter("status <> 3")
            .HasDatabaseName("ix_tenant_status");
    }
}
