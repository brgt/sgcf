using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento EF Core da entidade <see cref="Proposta"/> para a tabela <c>sgcf.proposta</c>.
/// SPEC §8.1.
///
/// Invariante: Proposta só é criada via <c>Cotacao.AdicionarProposta</c>.
/// O construtor privado do EF Core é mapeado sem configuração adicional.
/// </summary>
internal sealed class PropostaConfiguration : IEntityTypeConfiguration<Proposta>
{
    public void Configure(EntityTypeBuilder<Proposta> builder)
    {
        builder.ToTable("proposta");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(p => p.CotacaoId)
            .HasColumnName("cotacao_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.HasIndex(p => p.CotacaoId);

        builder.Property(p => p.BancoId)
            .HasColumnName("banco_id")
            .HasColumnType("uuid")
            .IsRequired();
        builder.HasIndex(p => p.BancoId);

        // FK: proposta.banco_id → banco_config.id (Restrict — não apagar banco com proposta ativa)
        builder.HasOne<Domain.Bancos.Banco>()
            .WithMany()
            .HasForeignKey(p => p.BancoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(p => p.MoedaOriginal)
            .HasColumnName("moeda_original")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(p => p.ValorOferecidoMoedaOriginalDecimal)
            .HasColumnName("valor_oferecido_moeda_original")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(p => p.TaxaAaPercentualDecimal)
            .HasColumnName("taxa_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(p => p.IofPercentualDecimal)
            .HasColumnName("iof_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(p => p.SpreadAaPercentualDecimal)
            .HasColumnName("spread_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired();

        builder.Property(p => p.PrazoDias)
            .HasColumnName("prazo_dias")
            .HasColumnType("integer")
            .IsRequired();

        builder.Property(p => p.EstruturaAmortizacao)
            .HasColumnName("estrutura_amortizacao")
            .HasConversion(new ValueConverter<EstruturaAmortizacao, short>(
                v => (short)v,
                v => (EstruturaAmortizacao)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(p => p.PeriodicidadeJuros)
            .HasColumnName("periodicidade_juros")
            .HasConversion(new ValueConverter<Periodicidade, short>(
                v => (short)v,
                v => (Periodicidade)v))
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(p => p.ExigeNdf)
            .HasColumnName("exige_ndf")
            .IsRequired();

        builder.Property(p => p.CustoNdfAaPercentualDecimal)
            .HasColumnName("custo_ndf_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(p => p.GarantiaExigida)
            .HasColumnName("garantia_exigida")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(p => p.ValorGarantiaExigidaBrlDecimal)
            .HasColumnName("valor_garantia_exigida_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(p => p.GarantiaEhCdbCativo)
            .HasColumnName("garantia_eh_cdb_cativo")
            .IsRequired();

        builder.Property(p => p.RendimentoCdbAaPercentualDecimal)
            .HasColumnName("rendimento_cdb_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(p => p.CetCalculadoAaPercentual)
            .HasColumnName("cet_calculado_aa_percentual")
            .HasColumnType("numeric(10,6)")
            .IsRequired(false);

        builder.Property(p => p.ValorTotalEstimadoBrlDecimal)
            .HasColumnName("valor_total_estimado_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(p => p.DataCaptura)
            .HasColumnName("data_captura")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(p => p.DataValidadeMercado)
            .HasColumnName("data_validade_mercado")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion(new ValueConverter<StatusProposta, short>(
                v => (short)v,
                v => (StatusProposta)v))
            .HasColumnType("smallint")
            .IsRequired();
        builder.HasIndex(p => p.Status);

        builder.Property(p => p.MotivoRecusa)
            .HasColumnName("motivo_recusa")
            .HasColumnType("text")
            .IsRequired(false);

        // Propriedades computadas (Money wrappers) não são persistidas.
        builder.Ignore(p => p.ValorOferecidoMoedaOriginal);
        builder.Ignore(p => p.TaxaAaPercentual);
        builder.Ignore(p => p.IofPercentual);
        builder.Ignore(p => p.SpreadAaPercentual);
        builder.Ignore(p => p.CustoNdfAaPercentual);
        builder.Ignore(p => p.ValorGarantiaExigidaBrl);
        builder.Ignore(p => p.RendimentoCdbAaPercentual);
        builder.Ignore(p => p.ValorTotalEstimadoBrl);
    }
}
