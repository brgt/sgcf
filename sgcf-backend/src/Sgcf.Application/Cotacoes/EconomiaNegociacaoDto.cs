using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public sealed record EconomiaNegociacaoDto(
    Guid Id,
    Guid CotacaoId,
    Guid ContratoId,
    decimal CetPropostaAaPercentual,
    decimal CetContratoAaPercentual,
    decimal EconomiaBrl,
    decimal EconomiaAjustadaCdiBrl,
    DateOnly DataReferenciaCdi,
    DateTimeOffset CreatedAt)
{
    public static EconomiaNegociacaoDto From(EconomiaNegociacao e) => new(
        e.Id,
        e.CotacaoId,
        e.ContratoId,
        e.CetPropostaAaPercentual,
        e.CetContratoAaPercentual,
        e.EconomiaBrl.Valor,
        e.EconomiaAjustadaCdiBrl.Valor,
        new DateOnly(e.DataReferenciaCdi.Year, e.DataReferenciaCdi.Month, e.DataReferenciaCdi.Day),
        e.CreatedAt.ToDateTimeOffset());
}
