using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Infrastructure.Persistence.Configurations;

internal sealed class EventoCronogramaConfiguration : IEntityTypeConfiguration<EventoCronograma>
{
    private static readonly ValueConverter<TipoEventoCronograma, string> TipoConverter =
        new(
            t => t == TipoEventoCronograma.Principal ? "principal"
               : t == TipoEventoCronograma.Juros ? "juros"
               : t == TipoEventoCronograma.IrrfRetido ? "irrf_retido"
               : t == TipoEventoCronograma.IofCambio ? "iof_cambio"
               : t == TipoEventoCronograma.ComissaoSblc ? "comissao_sblc"
               : t == TipoEventoCronograma.ComissaoCpg ? "comissao_cpg"
               : t == TipoEventoCronograma.ComissaoGarantiaFgi ? "comissao_garantia_fgi"
               : t == TipoEventoCronograma.TarifaRof ? "tarifa_rof"
               : t == TipoEventoCronograma.TarifaCademp ? "tarifa_cademp"
               : t == TipoEventoCronograma.TarifaCartorio ? "tarifa_cartorio"
               : t == TipoEventoCronograma.BreakFundingFee ? "break_funding_fee"
               : "multa_moratoria",
            s => s == "principal" ? TipoEventoCronograma.Principal
               : s == "juros" ? TipoEventoCronograma.Juros
               : s == "irrf_retido" ? TipoEventoCronograma.IrrfRetido
               : s == "iof_cambio" ? TipoEventoCronograma.IofCambio
               : s == "comissao_sblc" ? TipoEventoCronograma.ComissaoSblc
               : s == "comissao_cpg" ? TipoEventoCronograma.ComissaoCpg
               : s == "comissao_garantia_fgi" ? TipoEventoCronograma.ComissaoGarantiaFgi
               : s == "tarifa_rof" ? TipoEventoCronograma.TarifaRof
               : s == "tarifa_cademp" ? TipoEventoCronograma.TarifaCademp
               : s == "tarifa_cartorio" ? TipoEventoCronograma.TarifaCartorio
               : s == "break_funding_fee" ? TipoEventoCronograma.BreakFundingFee
               : TipoEventoCronograma.MultaMoratoria);

    private static readonly ValueConverter<StatusEventoCronograma, string> StatusConverter =
        new(
            s => s == StatusEventoCronograma.Previsto ? "previsto"
               : s == StatusEventoCronograma.Pago ? "pago"
               : s == StatusEventoCronograma.Atrasado ? "atrasado"
               : s == StatusEventoCronograma.Refinanciado ? "refinanciado"
               : "cancelado",
            s => s == "previsto" ? StatusEventoCronograma.Previsto
               : s == "pago" ? StatusEventoCronograma.Pago
               : s == "atrasado" ? StatusEventoCronograma.Atrasado
               : s == "refinanciado" ? StatusEventoCronograma.Refinanciado
               : StatusEventoCronograma.Cancelado);

    public void Configure(EntityTypeBuilder<EventoCronograma> builder)
    {
        builder.ToTable("cronograma_pagamento", t => t.HasCheckConstraint("ck_cronograma_valor_nao_negativo", "valor_moeda_original >= 0"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();

        builder.Property(e => e.ContratoId)
            .HasColumnName("contrato_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.NumeroEvento)
            .HasColumnName("numero_evento")
            .HasColumnType("smallint")
            .IsRequired();

        builder.Property(e => e.Tipo)
            .HasColumnName("tipo")
            .HasConversion(TipoConverter)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.DataPrevista)
            .HasColumnName("data_prevista")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(e => e.ValorMoedaOriginalDecimal)
            .HasColumnName("valor_moeda_original")
            .HasColumnType("numeric(20,6)")
            .IsRequired();

        builder.Property(e => e.ValorBrlEstimadoDecimal)
            .HasColumnName("valor_brl_estimado")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(e => e.SaldoDevedorAposDecimal)
            .HasColumnName("saldo_devedor_apos")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(e => e.Moeda)
            .HasColumnName("moeda")
            .HasConversion(SgcfConverters.Moeda)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(StatusConverter)
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.DataPagamentoEfetivo)
            .HasColumnName("data_pagamento_efetivo")
            .HasColumnType("date")
            .IsRequired(false);

        builder.Property(e => e.ValorPagamentoEfetivoDecimal)
            .HasColumnName("valor_pagamento_efetivo")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(e => e.ValorPagamentoEfetivoBrlDecimal)
            .HasColumnName("valor_pagamento_efetivo_brl")
            .HasColumnType("numeric(20,6)")
            .IsRequired(false);

        builder.Property(e => e.TaxaCambioPagamentoDecimal)
            .HasColumnName("taxa_cambio_pagamento")
            .HasColumnType("numeric(12,6)")
            .IsRequired(false);

        builder.Property(e => e.Observacoes)
            .HasColumnName("observacoes")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(e => e.ComprovanteUrl)
            .HasColumnName("comprovante_url")
            .HasColumnType("text")
            .IsRequired(false);

        builder.HasIndex(e => new { e.ContratoId, e.DataPrevista, e.Tipo });
        builder.HasIndex(e => new { e.ContratoId, e.NumeroEvento });

        builder.Ignore(e => e.ValorMoedaOriginal);
        builder.Ignore(e => e.ValorBrlEstimado);
        builder.Ignore(e => e.SaldoDevedorApos);
        builder.Ignore(e => e.ValorPagamentoEfetivo);
        builder.Ignore(e => e.ValorPagamentoEfetivoBrl);

        builder.HasOne<Sgcf.Domain.Contratos.Contrato>()
            .WithMany(c => c.EventosCronograma)
            .HasForeignKey(e => e.ContratoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
