using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio;

/// <summary>
/// Projeção de leitura de uma <see cref="CotacaoFx"/>. Expõe o momento como
/// <see cref="DateTimeOffset"/> (UTC) para serialização JSON estável.
/// </summary>
public sealed record CotacaoFxDto(
    string MoedaBase,
    string MoedaQuote,
    DateTimeOffset Momento,
    string Tipo,
    decimal ValorCompra,
    decimal ValorVenda,
    string Fonte)
{
    public static CotacaoFxDto From(CotacaoFx c) => new(
        c.MoedaBase.ToString(),
        c.MoedaQuote.ToString(),
        c.Momento.ToDateTimeOffset(),
        c.Tipo.ToString(),
        c.ValorCompra.Valor,
        c.ValorVenda.Valor,
        c.Fonte);
}
