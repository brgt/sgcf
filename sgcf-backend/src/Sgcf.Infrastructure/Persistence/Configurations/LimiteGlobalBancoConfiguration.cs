using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="LimiteGlobalBanco"/> para a tabela
/// <c>sgcf.limite_global_banco</c>.
///
/// Representa o limite guarda-chuva (umbrella) por banco, independente de modalidade.
/// SPEC §6.1, AD-05 — índice único parcial garante no máximo um registro vigente por banco/tenant.
/// </summary>
internal sealed class LimiteGlobalBancoConfiguration : IEntityTypeConfiguration<LimiteGlobalBanco>
{
    public void Configure(EntityTypeBuilder<LimiteGlobalBanco> builder)
    {
        builder.ToTable("limite_global_banco");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(l => l.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.ValorLimiteBrlDecimal)
            .HasColumnName("valor_limite_brl")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(l => l.DataVigenciaInicio)
            .HasColumnName("data_vigencia_inicio")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(l => l.DataVigenciaFim)
            .HasColumnName("data_vigencia_fim")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(l => l.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(l => l.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // FK: limite_global_banco.banco_id → banco_config.id (Restrict).
        // Restrição em vez de Cascade: excluir o banco não deve silenciosamente
        // remover os registros de limite global associados.
        builder.HasOne<Domain.Bancos.Banco>()
            .WithMany()
            .HasForeignKey(l => l.BancoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Histórico de alterações do valor: cascade delete (filhas acompanham o limite).
        builder.HasMany(l => l.Historico)
            .WithOne()
            .HasForeignKey(h => h.LimiteGlobalBancoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(LimiteGlobalBanco.Historico))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // AD-05 / LG-04: garante no máximo um registro vigente por (tenant, banco).
        // O filtro parcial exclui registros encerrados (data_vigencia_fim NOT NULL),
        // permitindo que o histórico de vigências anteriores coexista sem conflito.
        builder.HasIndex(l => new { l.TenantId, l.BancoId })
            .IsUnique()
            .HasFilter("data_vigencia_fim IS NULL")
            .HasDatabaseName("ix_limite_global_banco_banco_vigente_uq");

        builder.HasIndex(l => l.BancoId)
            .HasDatabaseName("ix_limite_global_banco_banco_id");

        builder.HasIndex(l => l.TenantId)
            .HasDatabaseName("ix_limite_global_banco_tenant_id");

        // Propriedade computada não é persistida.
        builder.Ignore(l => l.ValorLimiteBrl);
    }
}
