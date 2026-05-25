using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

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

/// <summary>
/// DTO de <see cref="CapitalDeGiroDetail"/> — Onda 3b.
/// Campos TipoProduto e TemFgi foram removidos (SPEC §3.3 e §3.4).
/// </summary>
public sealed record CapitalDeGiroDetailDto(string? NumeroOperacao);

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
    string Periodicidade,
    string EstruturaAmortizacao,
    int QuantidadeParcelas,
    DateOnly DataPrimeiroVencimento,
    string AnchorDiaMes,
    int? AnchorDiaFixo,
    string? PeriodicidadeJuros,
    string ConvencaoDataNaoUtil,
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
    CapitalDeGiroDetailDto? CapitalDeGiroDetail,
    FgiDetailDto? FgiDetail,
    // ── Rastreabilidade da política do banco (SC-01..SC-03) ─────────────────
    // Preenchidos na conversão cotação→contrato. Null para contratos pré-feature
    // ou em bancos sem LimiteBanco cadastrado (SC-06/SC-07). SPEC §3.5.
    Guid? LimiteBancoId,
    Guid? LimiteGlobalBancoId,
    Guid? GarantiasExigidasRevisaoId,
    // Snapshot dos itens da revisão vigente no momento da contratação.
    // Populado apenas em GET /contratos/{id} (detalhe). Null em listagem. SPEC §5.2.
    IReadOnlyList<GarantiaExigidaSnapshotItemDto>? GarantiasExigidasSnapshot)
{
    /// <summary>
    /// Constrói <see cref="ContratoDto"/> a partir da entidade de domínio.
    /// </summary>
    /// <param name="c">Entidade Contrato.</param>
    /// <param name="detail">Detail FINIMP (null se outra modalidade).</param>
    /// <param name="lei4131Detail">Detail Lei 4131 (null se outra modalidade).</param>
    /// <param name="refinimpDetail">Detail REFINIMP (null se outra modalidade).</param>
    /// <param name="nceDetail">Detail NCE (null se outra modalidade).</param>
    /// <param name="capitalDeGiroDetail">Detail Capital de Giro (null se outra modalidade).</param>
    /// <param name="fgiDetail">Detail FGI (null se outra modalidade).</param>
    /// <param name="snapshotItens">
    /// Itens da <c>GarantiaExigidaRevisao</c> apontada por <c>GarantiasExigidasRevisaoId</c>.
    /// Passar null (padrão) em listagem — economiza eager-load. Passar os itens em detalhe (SPEC §5.2).
    /// </param>
    public static ContratoDto From(
        Contrato c,
        FinimpDetail? detail,
        Lei4131Detail? lei4131Detail = null,
        RefinimpDetail? refinimpDetail = null,
        NceDetail? nceDetail = null,
        CapitalDeGiroDetail? capitalDeGiroDetail = null,
        FgiDetail? fgiDetail = null,
        IReadOnlyCollection<GarantiaExigidaItem>? snapshotItens = null)
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

        CapitalDeGiroDetailDto? capitalDeGiroDto = capitalDeGiroDetail is null
            ? null
            : new CapitalDeGiroDetailDto(capitalDeGiroDetail.NumeroOperacao);

        FgiDetailDto? fgiDto = fgiDetail is null
            ? null
            : new FgiDetailDto(
                fgiDetail.NumeroOperacaoFgi,
                fgiDetail.TaxaFgiAa.HasValue ? fgiDetail.TaxaFgiAa.Value.AsHumano : (decimal?)null,
                fgiDetail.PercentualCoberto.HasValue ? fgiDetail.PercentualCoberto.Value.AsHumano : (decimal?)null);

        // Snapshot: converte GarantiaExigidaItem → GarantiaExigidaSnapshotItemDto.
        // snapshotItens == null em listagem (performance) e em contratos pré-feature (sem revisão).
        IReadOnlyList<GarantiaExigidaSnapshotItemDto>? snapshotDto = null;
        if (snapshotItens is not null)
        {
            var itensDto = new List<GarantiaExigidaSnapshotItemDto>(snapshotItens.Count);
            foreach (GarantiaExigidaItem item in snapshotItens)
            {
                itensDto.Add(new GarantiaExigidaSnapshotItemDto(
                    Tipo: item.Tipo.ToString(),
                    PercentualSobreLimite: item.PercentualSobreLimite,
                    ValorFixoBrl: item.ValorFixoBrl?.Valor,
                    Obrigatoria: item.Obrigatoria,
                    Observacoes: item.Observacoes));
            }
            snapshotDto = itensDto.AsReadOnly();
        }

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
            c.Periodicidade.ToString(),
            c.EstruturaAmortizacao.ToString(),
            c.QuantidadeParcelas,
            new DateOnly(c.DataPrimeiroVencimento.Year, c.DataPrimeiroVencimento.Month, c.DataPrimeiroVencimento.Day),
            c.AnchorDiaMes.ToString(),
            c.AnchorDiaFixo,
            c.PeriodicidadeJuros?.ToString(),
            c.ConvencaoDataNaoUtil.ToString(),
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
            capitalDeGiroDto,
            fgiDto,
            LimiteBancoId: c.LimiteBancoId,
            LimiteGlobalBancoId: c.LimiteGlobalBancoId,
            GarantiasExigidasRevisaoId: c.GarantiasExigidasRevisaoId,
            GarantiasExigidasSnapshot: snapshotDto);
    }
}
