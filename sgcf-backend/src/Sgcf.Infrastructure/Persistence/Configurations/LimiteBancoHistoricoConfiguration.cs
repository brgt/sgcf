using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="LimiteBancoHistorico"/> para a tabela
/// <c>sgcf.limite_banco_historico</c>.
///
/// Registra mudanças no valor concedido pelo banco para análise de tendência
/// (aumentos vs. reduções por banco/modalidade ao longo do tempo).
/// </summary>
internal sealed class LimiteBancoHistoricoConfiguration : IEntityTypeConfiguration<LimiteBancoHistorico>
{
    public void Configure(EntityTypeBuilder<LimiteBancoHistorico> builder)
    {
        builder.ToTable("limite_banco_historico");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(h => h.LimiteBancoId)
            .HasColumnName("limite_banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(h => h.ValorAnteriorBrlDecimal)
            .HasColumnName("valor_anterior_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(h => h.ValorNovoBrlDecimal)
            .HasColumnName("valor_novo_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(h => h.RegistradoEm)
            .HasColumnName("registrado_em")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(h => h.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasIndex(h => h.LimiteBancoId)
            .HasDatabaseName("ix_limite_banco_historico_limite");

        builder.HasIndex(h => new { h.LimiteBancoId, h.RegistradoEm })
            .HasDatabaseName("ix_limite_banco_historico_limite_registrado_em");

        // Propriedades computadas não persistem.
        builder.Ignore(h => h.ValorAnteriorBrl);
        builder.Ignore(h => h.ValorNovoBrl);
    }
}
