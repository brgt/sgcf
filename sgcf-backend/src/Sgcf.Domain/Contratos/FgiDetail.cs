using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de contrato FGI (Fundo Garantidor de Investimentos) — programa de garantia Caixa/BV.
/// Tabela de extensão 1:1 com <see cref="Contrato"/> — mesma convenção de FinimpDetail e Lei4131Detail.
/// <para>
/// A taxa FGI (<see cref="TaxaFgiAa"/>) é cobrada anualmente sobre o saldo devedor e gera eventos
/// do tipo <c>TarifaFgi</c> no cronograma junto à amortização bullet.
/// </para>
/// </summary>
public sealed class FgiDetail : Entity
{
    public Guid ContratoId { get; private set; }

    /// <summary>Número da operação no sistema FGI.</summary>
    public string? NumeroOperacaoFgi { get; private set; }

    /// <summary>
    /// Taxa de garantia FGI anual armazenada como fração (0..1).
    /// Null quando não informada.
    /// </summary>
    internal decimal? TaxaFgiAaDecimal { get; private set; }

    /// <summary>Taxa de garantia FGI anual como <see cref="Percentual"/> tipado. Null quando não informada.</summary>
    public Percentual? TaxaFgiAa => TaxaFgiAaDecimal.HasValue
        ? Percentual.DeFracao(TaxaFgiAaDecimal.Value)
        : (Percentual?)null;

    /// <summary>
    /// Percentual do principal coberto pela garantia FGI, armazenado como fração (0..1).
    /// Null quando não informado.
    /// </summary>
    internal decimal? PercentualCobertoBacking { get; private set; }

    /// <summary>Percentual do principal coberto pela garantia FGI. Null quando não informado.</summary>
    public Percentual? PercentualCoberto => PercentualCobertoBacking.HasValue
        ? Percentual.DeFracao(PercentualCobertoBacking.Value)
        : (Percentual?)null;

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private FgiDetail() { }

    /// <summary>
    /// Cria um novo <see cref="FgiDetail"/>.
    /// <paramref name="taxaFgiAaPct"/> e <paramref name="percentualCobertoPct"/> são fornecidos como
    /// percentual humano (ex: 1.5 para 1,5%) e convertidos para fração internamente.
    /// </summary>
    public static FgiDetail Criar(
        Guid contratoId,
        string? numeroOperacaoFgi,
        decimal? taxaFgiAaPct,
        decimal? percentualCobertoPct,
        IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new FgiDetail
        {
            ContratoId = contratoId,
            NumeroOperacaoFgi = numeroOperacaoFgi,
            TaxaFgiAaDecimal = taxaFgiAaPct.HasValue
                ? taxaFgiAaPct.Value / 100m
                : (decimal?)null,
            PercentualCobertoBacking = percentualCobertoPct.HasValue
                ? percentualCobertoPct.Value / 100m
                : (decimal?)null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
