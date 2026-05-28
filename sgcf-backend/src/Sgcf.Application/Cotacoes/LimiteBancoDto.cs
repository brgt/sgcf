using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public sealed record LimiteBancoDto(
    Guid Id,
    Guid BancoId,
    string Modalidade,
    decimal ValorLimiteBrl,
    decimal ValorUtilizadoBrl,
    decimal ValorDisponivelBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim,
    string? Observacoes,
    string? MotivoEncerramento,
    string? PadraoAntecipacao,
    decimal? BreakFundingFeePct,
    decimal? TlaPctSobreSaldo,
    decimal? TlaPctPorMesRemanescente,
    decimal? ValorMinimoParcialPct,
    string? ObservacoesAntecipacao,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GarantiaExigidaItemDto> GarantiasExigidas,
    IReadOnlyList<LimiteBancoHistoricoDto> Historico)
{
    public static LimiteBancoDto From(LimiteBanco l)
    {
        List<GarantiaExigidaItemDto> garantias = new(l.GarantiasExigidas.Count);
        foreach (GarantiaExigidaItem g in l.GarantiasExigidas)
        {
            garantias.Add(GarantiaExigidaItemDto.From(g));
        }

        List<LimiteBancoHistoricoDto> historico = new(l.Historico.Count);
        foreach (LimiteBancoHistorico h in l.Historico.OrderBy(h => h.RegistradoEm))
        {
            historico.Add(LimiteBancoHistoricoDto.From(h));
        }

        return new LimiteBancoDto(
            l.Id,
            l.BancoId,
            l.Modalidade.ToString(),
            l.ValorLimiteBrl.Valor,
            l.ValorUtilizadoBrl.Valor,
            l.ValorDisponivelBrl.Valor,
            new DateOnly(l.DataVigenciaInicio.Year, l.DataVigenciaInicio.Month, l.DataVigenciaInicio.Day),
            l.DataVigenciaFim.HasValue
                ? new DateOnly(l.DataVigenciaFim.Value.Year, l.DataVigenciaFim.Value.Month, l.DataVigenciaFim.Value.Day)
                : null,
            l.Observacoes,
            l.MotivoEncerramento,
            l.PadraoAntecipacao?.ToString(),
            l.BreakFundingFeePct.HasValue ? l.BreakFundingFeePct.Value.AsHumano : (decimal?)null,
            l.TlaPctSobreSaldo.HasValue ? l.TlaPctSobreSaldo.Value.AsHumano : (decimal?)null,
            l.TlaPctPorMesRemanescente.HasValue ? l.TlaPctPorMesRemanescente.Value.AsHumano : (decimal?)null,
            l.ValorMinimoParcialPct.HasValue ? l.ValorMinimoParcialPct.Value.AsHumano : (decimal?)null,
            l.ObservacoesAntecipacao,
            l.CreatedAt.ToDateTimeOffset(),
            l.UpdatedAt.ToDateTimeOffset(),
            garantias.AsReadOnly(),
            historico.AsReadOnly());
    }
}
