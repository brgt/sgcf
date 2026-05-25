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
///
/// T0.2 (S34): navegação <c>GarantiasExigidas</c> (HasMany direto de LimiteBanco →
/// GarantiaExigidaItem) removida. A nova hierarquia passa por
/// <c>RevisoesGarantiasExigidas</c> → <c>GarantiaExigidaRevisao</c> → <c>Itens</c>.
/// O HasMany de GarantiaExigidaItem agora está em GarantiaExigidaRevisaoConfiguration.
/// </summary>
internal sealed class LimiteBancoConfiguration : IEntityTypeConfiguration<LimiteBanco>
{
    public void Configure(EntityTypeBuilder<LimiteBanco> builder)
    {
        builder.ToTable("limite_banco");

        builder.HasKey(l => l.Id);
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").HasColumnType("uuid").IsRequired();
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

        // Revisões de garantias exigidas (S34): cascade delete.
        // Campo privado _revisoesGarantias não segue a convenção de nome da propriedade
        // pública (RevisoesGarantiasExigidas), então é preciso informar o field explicitamente.
        builder.Navigation(l => l.RevisoesGarantiasExigidas)
            .HasField("_revisoesGarantias")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(l => l.RevisoesGarantiasExigidas)
            .WithOne()
            .HasForeignKey(r => r.LimiteBancoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Histórico de valores concedidos: cascade delete.
        builder.HasMany(l => l.Historico)
            .WithOne()
            .HasForeignKey(h => h.LimiteBancoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(LimiteBanco.Historico))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Propriedades computadas — não persistidas.
        builder.Ignore(l => l.GarantiasExigidas);
        builder.Ignore(l => l.RevisaoGarantiasVigente);

        // ── Configuração de antecipação por modalidade (S32) ─────────────────────
        builder.Property(l => l.PadraoAntecipacao)
            .HasColumnName("padrao_antecipacao")
            .HasColumnType("smallint")
            .HasConversion(SgcfConverters.PadraoAntecipacao)
            .IsRequired(false);

        builder.Property(l => l.BreakFundingFeePctDecimal)
            .HasColumnName("break_funding_fee_pct")
            .HasColumnType("numeric(18,6)")
            .IsRequired(false);

        builder.Property(l => l.TlaPctSobreSaldoDecimal)
            .HasColumnName("tla_pct_sobre_saldo")
            .HasColumnType("numeric(18,6)")
            .IsRequired(false);

        builder.Property(l => l.TlaPctPorMesRemanescenteDecimal)
            .HasColumnName("tla_pct_por_mes_remanescente")
            .HasColumnType("numeric(18,6)")
            .IsRequired(false);

        builder.Property(l => l.ValorMinimoParcialPctDecimal)
            .HasColumnName("valor_minimo_parcial_pct")
            .HasColumnType("numeric(18,6)")
            .IsRequired(false);

        builder.Property(l => l.ObservacoesAntecipacao)
            .HasColumnName("observacoes_antecipacao")
            .HasColumnType("text")
            .IsRequired(false);

        // Propriedades computadas não são persistidas.
        builder.Ignore(l => l.ValorLimiteBrl);
        builder.Ignore(l => l.ValorUtilizadoBrl);
        builder.Ignore(l => l.ValorDisponivelBrl);
        builder.Ignore(l => l.BreakFundingFeePct);
        builder.Ignore(l => l.TlaPctSobreSaldo);
        builder.Ignore(l => l.TlaPctPorMesRemanescente);
        builder.Ignore(l => l.ValorMinimoParcialPct);
    }
}
