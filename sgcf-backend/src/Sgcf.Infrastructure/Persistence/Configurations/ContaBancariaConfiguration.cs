using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class ContaBancariaConfiguration : IEntityTypeConfiguration<ContaBancaria>
{
    public void Configure(EntityTypeBuilder<ContaBancaria> builder)
    {
        builder.ToTable("conta_bancaria");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(c => c.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.Nome)
            .HasColumnName("nome")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Agencia)
            .HasColumnName("agencia")
            .HasColumnType("varchar(10)")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(c => c.NumeroConta)
            .HasColumnName("numero_conta")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Moeda)
            .HasColumnName("moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(c => c.Ativa)
            .HasColumnName("ativa")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.CriadoEm)
            .HasColumnName("criado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.AtualizadoEm)
            .HasColumnName("atualizado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Soft delete query filter — combinado com o filtro de tenant pelo DbContext.
        builder.HasQueryFilter(c => c.DeletedAt == null);

        // FK para o catálogo global Banco (sem navigation property para manter o domain limpo).
        builder.HasIndex(c => c.BancoId)
            .HasDatabaseName("ix_conta_bancaria_banco_id");

        // Unicidade por tenant + agência + número de conta — evita duplicidade de conta
        // dentro do mesmo tenant, mas permite que dois tenants tenham a mesma combinação.
        builder.HasIndex(c => new { c.TenantId, c.Agencia, c.NumeroConta })
            .IsUnique()
            .HasDatabaseName("ux_conta_bancaria_tenant_agencia_numero");
    }
}
