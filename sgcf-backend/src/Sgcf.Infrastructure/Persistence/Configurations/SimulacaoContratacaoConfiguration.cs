using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade filha <see cref="SimulacaoContratacao"/>
/// para a tabela <c>sgcf.simulacao_contratacao</c>.
///
/// Decisões de design:
/// - Money: ValorPrincipalDecimal (numeric) + ValorPrincipalMoedaInt (smallint).
///   A propriedade computada ValorPrincipal é ignorada pelo EF.
///   ValorPrincipalMoedaInt é internal (InternalsVisibleTo("Sgcf.Infrastructure")).
/// - Percentual (TaxaAa, SpreadAa): armazenado como fração decimal (0.025 = 2,5%).
///   Nullable — apenas um dos dois é preenchido por TipoTaxa.
/// - Enums via short: Periodicidade, EstruturaAmortizacao, AnchorDiaMes seguem
///   o padrão de ContratoConfiguration (ValueConverter<TEnum, short>).
/// - BaseCalculo via SgcfConverters.BaseCalculo (short converter existente).
/// - Modalidade e BaseCalculo via converters centralizados em SgcfConverters.
/// - Sem FK física para banco_id (banco pode ser de sistema externo futuramente).
/// </summary>
internal sealed class SimulacaoContratacaoConfiguration : IEntityTypeConfiguration<SimulacaoContratacao>
{
    public void Configure(EntityTypeBuilder<SimulacaoContratacao> builder)
    {
        builder.ToTable("simulacao_contratacao");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(s => s.CenarioId)
            .HasColumnName("cenario_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(s => s.Modalidade)
            .HasColumnName("modalidade")
            .HasConversion(SgcfConverters.Modalidade)
            .HasColumnType("text")
            .IsRequired();

        // Money: coluna principal armazena a moeda como código inteiro (enum int).
        // ValorPrincipalMoedaInt é internal — acessível via InternalsVisibleTo("Sgcf.Infrastructure").
        // Moeda pública é propriedade calculada (retorna (Moeda)ValorPrincipalMoedaInt) — ignorada pelo EF.
        builder.Property(s => s.ValorPrincipalDecimal)
            .HasColumnName("valor_principal")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(s => s.ValorPrincipalMoedaInt)
            .HasColumnName("moeda")
            .HasColumnType("smallint")
            .IsRequired();

        // Propriedades calculadas/redundantes — não persistidas.
        builder.Ignore(s => s.ValorPrincipal);
        builder.Ignore(s => s.Moeda);

        builder.Property(s => s.DataContratacaoPrevista)
            .HasColumnName("data_contratacao_prevista")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(s => s.DataPrimeiroVencimento)
            .HasColumnName("data_primeiro_vencimento")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(s => s.TipoTaxa)
            .HasColumnName("tipo_taxa")
            .HasConversion<short>()
            .HasColumnType("smallint")
            .IsRequired();

        // Percentual armazenado como fração decimal (0.025 para 2,5%).
        // Conversor extrai/restaura o campo AsDecimal do value object.
        ValueConverter<Percentual?, decimal?> percentualConverter = new(
            p => p.HasValue ? p.Value.AsDecimal : (decimal?)null,
            d => d.HasValue ? Percentual.DeFracao(d.Value) : (Percentual?)null);

        builder.Property(s => s.TaxaAa)
            .HasColumnName("taxa_aa")
            .HasConversion(percentualConverter)
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(s => s.SpreadAa)
            .HasColumnName("spread_aa")
            .HasConversion(percentualConverter)
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(s => s.BaseCalculo)
            .HasColumnName("base_calculo")
            .HasConversion(SgcfConverters.BaseCalculo)
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.EstruturaAmortizacao)
            .HasColumnName("estrutura_amortizacao")
            .HasConversion(new ValueConverter<EstruturaAmortizacao, short>(v => (short)v, v => (EstruturaAmortizacao)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.Periodicidade)
            .HasColumnName("periodicidade")
            .HasConversion(new ValueConverter<Periodicidade, short>(v => (short)v, v => (Periodicidade)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.QuantidadeParcelas)
            .HasColumnName("quantidade_parcelas")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(s => s.AnchorDiaMes)
            .HasColumnName("anchor_dia_mes")
            .HasConversion(new ValueConverter<AnchorDiaMes, short>(v => (short)v, v => (AnchorDiaMes)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(s => s.AnchorDiaFixo)
            .HasColumnName("anchor_dia_fixo")
            .HasColumnType("int")
            .IsRequired(false);

        builder.Property(s => s.GarantiaExigidaPrevista)
            .HasColumnName("garantia_exigida_prevista")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(s => s.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(s => s.Version)
            .HasColumnName("version")
            .HasColumnType("int")
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(s => s.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // Índices para queries comuns de leitura.
        builder.HasIndex(s => s.CenarioId);
        builder.HasIndex(s => s.BancoId);
    }
}
