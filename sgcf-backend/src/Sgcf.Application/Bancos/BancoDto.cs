using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos;

public sealed record BancoDto(
    Guid Id,
    string CodigoCompe,
    string RazaoSocial,
    string Apelido,
    bool AceitaLiquidacaoTotal,
    bool AceitaLiquidacaoParcial,
    bool ExigeAnuenciaExpressa,
    bool ExigeParcelaInteira,
    int AvisoPrevioMinDiasUteis,
    string PadraoAntecipacao,
    decimal? ValorMinimoParcialPct,
    decimal? BreakFundingFeePct,
    decimal? TlaPctSobreSaldo,
    decimal? TlaPctPorMesRemanescente,
    string? ObservacoesAntecipacao,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static BancoDto From(Banco banco) =>
        new(
            banco.Id,
            banco.CodigoCompe,
            banco.RazaoSocial,
            banco.Apelido,
            banco.AceitaLiquidacaoTotal,
            banco.AceitaLiquidacaoParcial,
            banco.ExigeAnuenciaExpressa,
            banco.ExigeParcelaInteira,
            banco.AvisoPrevioMinDiasUteis,
            banco.PadraoAntecipacao.ToString(),
            banco.ValorMinimoParcialPct.HasValue ? banco.ValorMinimoParcialPct.Value.AsHumano : (decimal?)null,
            banco.BreakFundingFeePct.HasValue ? banco.BreakFundingFeePct.Value.AsHumano : (decimal?)null,
            banco.TlaPctSobreSaldo.HasValue ? banco.TlaPctSobreSaldo.Value.AsHumano : (decimal?)null,
            banco.TlaPctPorMesRemanescente.HasValue ? banco.TlaPctPorMesRemanescente.Value.AsHumano : (decimal?)null,
            banco.ObservacoesAntecipacao,
            banco.CreatedAt.ToDateTimeOffset(),
            banco.UpdatedAt.ToDateTimeOffset());
}
