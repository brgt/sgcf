using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="GarantiaExigidaItem"/> para a tabela
/// <c>sgcf.limite_banco_garantia_exigida</c>.
///
/// CHECK constraint XOR: (percentual IS NOT NULL) &lt;&gt; (valor_fixo IS NOT NULL),
/// relaxado para Aval (ambos podem ser nulos quando tipo = 3).
///
/// UNIQUE(limite_banco_id, tipo): espelha o invariante de domínio de que não há
/// duplicação por tipo dentro de um mesmo limite.
/// </summary>
internal sealed class GarantiaExigidaItemConfiguration : IEntityTypeConfiguration<GarantiaExigidaItem>
{
    public void Configure(EntityTypeBuilder<GarantiaExigidaItem> builder)
    {
        builder.ToTable("limite_banco_garantia_exigida", t =>
        {
            // XOR com relaxação para Aval (tipo = 3).
            t.HasCheckConstraint(
                "ck_garantia_exigida_percentual_xor_valor",
                "(percentual_sobre_limite IS NULL AND valor_fixo_brl IS NULL AND tipo = 3) "
                + "OR (percentual_sobre_limite IS NOT NULL AND valor_fixo_brl IS NULL) "
                + "OR (percentual_sobre_limite IS NULL AND valor_fixo_brl IS NOT NULL)");

            t.HasCheckConstraint(
                "ck_garantia_exigida_percentual_intervalo",
                "percentual_sobre_limite IS NULL OR (percentual_sobre_limite > 0 AND percentual_sobre_limite <= 100)");

            t.HasCheckConstraint(
                "ck_garantia_exigida_valor_fixo_positivo",
                "valor_fixo_brl IS NULL OR valor_fixo_brl > 0");
        });

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(g => g.LimiteBancoId)
            .HasColumnName("limite_banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(g => g.Tipo)
            .HasColumnName("tipo")
            .HasColumnType("integer")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(g => g.PercentualSobreLimite)
            .HasColumnName("percentual_sobre_limite")
            .HasColumnType("numeric(7,4)")
            .IsRequired(false);

        builder.Property(g => g.ValorFixoBrlDecimal)
            .HasColumnName("valor_fixo_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(g => g.Obrigatoria)
            .HasColumnName("obrigatoria")
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(g => g.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(g => g.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(g => g.LimiteBancoId)
            .HasDatabaseName("ix_garantia_exigida_limite_banco");

        builder.HasIndex(g => new { g.LimiteBancoId, g.Tipo })
            .IsUnique()
            .HasDatabaseName("ux_garantia_exigida_limite_tipo");

        // Propriedade computada não persiste.
        builder.Ignore(g => g.ValorFixoBrl);
    }
}
