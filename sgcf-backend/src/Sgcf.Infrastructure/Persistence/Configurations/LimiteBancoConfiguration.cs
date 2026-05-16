using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="LimiteBanco"/> para a tabela <c>sgcf.limite_banco</c>.
/// SPEC §3.1, §8.1.
///
/// Decisão: unicidade de (banco_id, modalidade) é enforced via índice único,
/// sem filtro de vigência — a Application garante a regra de um limite vigente por vez.
/// </summary>
internal sealed class LimiteBancoConfiguration : IEntityTypeConfiguration<LimiteBanco>
{
    public void Configure(EntityTypeBuilder<LimiteBanco> builder)
    {
        builder.ToTable("limite_banco");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(l => l.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(l => l.Modalidade)
            .HasColumnName("modalidade")
            .HasConversion(SgcfConverters.Modalidade)
            .HasColumnType("text")
            .IsRequired();

        // Unique por banco+modalidade: apenas um registro por combinação (vigência atual).
        // Histórico pode ser mantido via data_vigencia_fim, mas a Application controla isso.
        builder.HasIndex(l => new { l.BancoId, l.Modalidade })
            .IsUnique()
            .HasFilter("data_vigencia_fim IS NULL");

        builder.Property(l => l.ValorLimiteBrlDecimal)
            .HasColumnName("valor_limite_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(l => l.ValorUtilizadoBrlDecimal)
            .HasColumnName("valor_utilizado_brl")
            .HasColumnType("numeric(20,6)")
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

        // FK: limite_banco.banco_id → banco_config.id (Restrict)
        builder.HasOne<Domain.Bancos.Banco>()
            .WithMany()
            .HasForeignKey(l => l.BancoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Propriedades computadas não são persistidas.
        builder.Ignore(l => l.ValorLimiteBrl);
        builder.Ignore(l => l.ValorUtilizadoBrl);
        builder.Ignore(l => l.ValorDisponivelBrl);
    }
}
