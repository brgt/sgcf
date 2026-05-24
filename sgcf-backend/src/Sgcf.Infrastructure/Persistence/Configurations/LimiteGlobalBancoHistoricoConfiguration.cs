using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="LimiteGlobalBancoHistorico"/> para a tabela
/// <c>sgcf.limite_global_banco_historico</c>.
///
/// Registra mudanças no valor do limite guarda-chuva concedido pelo banco,
/// permitindo análise de tendência ao longo do tempo.
/// SPEC §6.1 — LimiteGlobalBancoHistorico.
/// </summary>
internal sealed class LimiteGlobalBancoHistoricoConfiguration : IEntityTypeConfiguration<LimiteGlobalBancoHistorico>
{
    public void Configure(EntityTypeBuilder<LimiteGlobalBancoHistorico> builder)
    {
        builder.ToTable("limite_global_banco_historico");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(h => h.LimiteGlobalBancoId)
            .HasColumnName("limite_global_banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.ValorAnteriorBrlDecimal)
            .HasColumnName("valor_anterior_brl")
            .HasColumnType("numeric(18,2)")
            .IsRequired(false);

        builder.Property(h => h.ValorNovoBrlDecimal)
            .HasColumnName("valor_novo_brl")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(h => h.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(h => h.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasIndex(h => h.LimiteGlobalBancoId)
            .HasDatabaseName("ix_limite_global_banco_historico_limite_id");

        builder.HasIndex(h => new { h.LimiteGlobalBancoId, h.RegistradoEm })
            .HasDatabaseName("ix_limite_global_banco_historico_limite_registrado_em");

        // Propriedades computadas não são persistidas.
        builder.Ignore(h => h.ValorAnteriorBrl);
        builder.Ignore(h => h.ValorNovoBrl);
    }
}
