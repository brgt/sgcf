using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo FGI (Fundo Garantidor de Investimentos) — extensão 1:1 com <see cref="Garantia"/>.
/// Não confundir com <see cref="FgiDetail"/> que é detalhe de <em>contrato</em> modalidade FGI.
/// </summary>
public sealed class GarantiaFgiDetail : Entity
{
    public Guid GarantiaId { get; private set; }

    /// <summary>Subtipo do FGI (ex: FGI_PEAC, FGI_NOVO_EMPREENDEDOR).</summary>
    public string TipoFgi { get; private set; } = default!;

    /// <summary>Percentual de cobertura FGI armazenado como fração (0..1).</summary>
    internal decimal PercentualCoberturaDecimal { get; private set; }

    /// <summary>Percentual do principal coberto pelo FGI.</summary>
    public Percentual PercentualCobertura => Percentual.DeFracao(PercentualCoberturaDecimal);

    /// <summary>Taxa de garantia anual do FGI armazenada como fração (0..1). Null quando não informada.</summary>
    internal decimal? TaxaFgiAaDecimal { get; private set; }

    /// <summary>Taxa de garantia anual do FGI. Null quando não informada.</summary>
    public Percentual? TaxaFgiAa =>
        TaxaFgiAaDecimal.HasValue ? Percentual.DeFracao(TaxaFgiAaDecimal.Value) : null;

    /// <summary>Banco intermediário que operacionaliza o FGI via BNDES. Null quando não informado.</summary>
    public string? BancoIntermediario { get; private set; }

    /// <summary>Código da operação no sistema BNDES. Null quando não disponível.</summary>
    public string? CodigoOperacaoBndes { get; private set; }

    private GarantiaFgiDetail() { }

    /// <summary>
    /// Cria um novo detalhe de garantia FGI.
    /// <paramref name="percentualCoberturaPct"/> e <paramref name="taxaFgiAaPct"/> são fornecidos
    /// como valor humano (ex: 80.0 para 80%).
    /// </summary>
    public static GarantiaFgiDetail Criar(
        Guid garantiaId,
        string tipoFgi,
        decimal percentualCoberturaPct,
        decimal? taxaFgiAaPct,
        string? bancoIntermediario,
        string? codigoOperacaoBndes)
    {
        if (string.IsNullOrWhiteSpace(tipoFgi))
        {
            throw new ArgumentException("TipoFgi não pode ser vazio.", nameof(tipoFgi));
        }

        if (percentualCoberturaPct <= 0m || percentualCoberturaPct > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentualCoberturaPct), "PercentualCobertura deve estar entre 0 e 100 (exclusivo).");
        }

        return new GarantiaFgiDetail
        {
            GarantiaId = garantiaId,
            TipoFgi = tipoFgi,
            PercentualCoberturaDecimal = percentualCoberturaPct / 100m,
            TaxaFgiAaDecimal = taxaFgiAaPct.HasValue ? taxaFgiAaPct.Value / 100m : (decimal?)null,
            BancoIntermediario = bancoIntermediario,
            CodigoOperacaoBndes = codigoOperacaoBndes
        };
    }
}
