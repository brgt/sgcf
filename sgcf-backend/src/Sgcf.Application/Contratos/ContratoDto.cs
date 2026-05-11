using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos;

public sealed record ParcelaDto(
    Guid Id,
    short Numero,
    DateOnly DataVencimento,
    decimal ValorPrincipal,
    decimal ValorJuros,
    decimal? ValorPago,
    string Moeda,
    string Status,
    DateOnly? DataPagamento);

/// <summary>
/// Resumo de garantia embutido em <see cref="ContratoDto"/>.
/// Contém apenas campos da tabela mestre — use o endpoint dedicado para detalhes polimórficos.
/// </summary>
public sealed record GarantiaResumoDto(
    Guid Id,
    string Tipo,
    decimal ValorBrl,
    decimal? PercentualPrincipalPct,
    DateOnly DataConstituicao,
    string Status);

public sealed record FinimpDetailDto(
    string? RofNumero,
    DateOnly? RofDataEmissao,
    string? ExportadorNome,
    string? ExportadorPais,
    string? ProdutoImportado,
    string? FaturaReferencia,
    string? Incoterm,
    decimal? BreakFundingFeePercentual,
    bool TemMarketFlex);

public sealed record Lei4131DetailDto(
    string? SblcNumero,
    string? SblcBancoEmissor,
    decimal? SblcValorUsd,
    bool TemMarketFlex,
    decimal? BreakFundingFeePercentual);

public sealed record RefinimpDetailDto(
    Guid ContratoMaeId,
    decimal PercentualRefinanciado,
    decimal ValorQuitadoNoRefi,
    string Moeda);

public sealed record NceDetailDto(
    string? NceNumero,
    DateOnly? DataEmissao,
    string? BancoMandatario);

public sealed record BalcaoCaixaDetailDto(
    string? NumeroOperacao,
    string? TipoProduto,
    bool TemFgi);

public sealed record FgiDetailDto(
    string? NumeroOperacaoFgi,
    decimal? TaxaFgiAaPct,
    decimal? PercentualCobertoPct);

public sealed record ContratoDto(
    Guid Id,
    string NumeroExterno,
    string? CodigoInterno,
    Guid BancoId,
    string Modalidade,
    string Moeda,
    decimal ValorPrincipal,
    DateOnly DataContratacao,
    DateOnly DataVencimento,
    decimal TaxaAa,
    string BaseCalculo,
    string Status,
    Guid? ContratoPaiId,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ParcelaDto> Parcelas,
    IReadOnlyList<GarantiaResumoDto> Garantias,
    FinimpDetailDto? FinimpDetail,
    Lei4131DetailDto? Lei4131Detail,
    RefinimpDetailDto? RefinimpDetail,
    NceDetailDto? NceDetail,
    BalcaoCaixaDetailDto? BalcaoCaixaDetail,
    FgiDetailDto? FgiDetail)
{
    public static ContratoDto From(
        Contrato c,
        FinimpDetail? detail,
        Lei4131Detail? lei4131Detail = null,
        RefinimpDetail? refinimpDetail = null,
        NceDetail? nceDetail = null,
        BalcaoCaixaDetail? balcaoCaixaDetail = null,
        FgiDetail? fgiDetail = null)
    {
        List<ParcelaDto> parcelas = new(c.Parcelas.Count);
        foreach (Parcela p in c.Parcelas)
        {
            parcelas.Add(new ParcelaDto(
                p.Id,
                p.Numero,
                new DateOnly(p.DataVencimento.Year, p.DataVencimento.Month, p.DataVencimento.Day),
                p.ValorPrincipal.Valor,
                p.ValorJuros.Valor,
                p.ValorPago?.Valor,
                p.Moeda.ToString(),
                p.Status.ToString(),
                p.DataPagamento.HasValue
                    ? new DateOnly(p.DataPagamento.Value.Year, p.DataPagamento.Value.Month, p.DataPagamento.Value.Day)
                    : (DateOnly?)null));
        }

        List<GarantiaResumoDto> garantias = new(c.Garantias.Count);
        foreach (Garantia g in c.Garantias)
        {
            garantias.Add(new GarantiaResumoDto(
                g.Id,
                g.Tipo.ToString(),
                g.ValorBrl.Valor,
                g.PercentualPrincipal?.AsHumano,
                new DateOnly(g.DataConstituicao.Year, g.DataConstituicao.Month, g.DataConstituicao.Day),
                g.Status.ToString()));
        }

        FinimpDetailDto? finimpDto = detail is null
            ? null
            : new FinimpDetailDto(
                detail.RofNumero,
                detail.RofDataEmissao.HasValue
                    ? new DateOnly(detail.RofDataEmissao.Value.Year, detail.RofDataEmissao.Value.Month, detail.RofDataEmissao.Value.Day)
                    : (DateOnly?)null,
                detail.ExportadorNome,
                detail.ExportadorPais,
                detail.ProdutoImportado,
                detail.FaturaReferencia,
                detail.Incoterm,
                detail.BreakFundingFeePercentual.HasValue
                    ? detail.BreakFundingFeePercentual.Value * 100m
                    : (decimal?)null,
                detail.TemMarketFlex);

        Lei4131DetailDto? lei4131Dto = lei4131Detail is null
            ? null
            : new Lei4131DetailDto(
                lei4131Detail.SblcNumero,
                lei4131Detail.SblcBancoEmissor,
                lei4131Detail.SblcValorUsd,
                lei4131Detail.TemMarketFlex,
                lei4131Detail.BreakFundingFeePercentual.HasValue
                    ? lei4131Detail.BreakFundingFeePercentual.Value * 100m
                    : (decimal?)null);

        RefinimpDetailDto? refinimpDto = refinimpDetail is null
            ? null
            : new RefinimpDetailDto(
                refinimpDetail.ContratoMaeId,
                refinimpDetail.PercentualRefinanciado.AsHumano,
                refinimpDetail.ValorQuitadoNoRefi.Valor,
                refinimpDetail.ValorQuitadoNoRefi.Moeda.ToString());

        NceDetailDto? nceDto = nceDetail is null
            ? null
            : new NceDetailDto(
                nceDetail.NceNumero,
                nceDetail.DataEmissao.HasValue
                    ? new DateOnly(nceDetail.DataEmissao.Value.Year, nceDetail.DataEmissao.Value.Month, nceDetail.DataEmissao.Value.Day)
                    : (DateOnly?)null,
                nceDetail.BancoMandatario);

        BalcaoCaixaDetailDto? balcaoCaixaDto = balcaoCaixaDetail is null
            ? null
            : new BalcaoCaixaDetailDto(
                balcaoCaixaDetail.NumeroOperacao,
                balcaoCaixaDetail.TipoProduto,
                balcaoCaixaDetail.TemFgi);

        FgiDetailDto? fgiDto = fgiDetail is null
            ? null
            : new FgiDetailDto(
                fgiDetail.NumeroOperacaoFgi,
                fgiDetail.TaxaFgiAa.HasValue ? fgiDetail.TaxaFgiAa.Value.AsHumano : (decimal?)null,
                fgiDetail.PercentualCoberto.HasValue ? fgiDetail.PercentualCoberto.Value.AsHumano : (decimal?)null);

        return new ContratoDto(
            c.Id,
            c.NumeroExterno,
            c.CodigoInterno,
            c.BancoId,
            c.Modalidade.ToString(),
            c.Moeda.ToString(),
            c.ValorPrincipal.Valor,
            new DateOnly(c.DataContratacao.Year, c.DataContratacao.Month, c.DataContratacao.Day),
            new DateOnly(c.DataVencimento.Year, c.DataVencimento.Month, c.DataVencimento.Day),
            c.TaxaAa.AsHumano,
            c.BaseCalculo.ToString(),
            c.Status.ToString(),
            c.ContratoPaiId,
            c.Observacoes,
            c.CreatedAt.ToDateTimeOffset(),
            c.UpdatedAt.ToDateTimeOffset(),
            parcelas.AsReadOnly(),
            garantias.AsReadOnly(),
            finimpDto,
            lei4131Dto,
            refinimpDto,
            nceDto,
            balcaoCaixaDto,
            fgiDto);
    }
}
