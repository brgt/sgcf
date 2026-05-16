using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public sealed record CotacaoDto(
    Guid Id,
    string CodigoInterno,
    string Modalidade,
    decimal ValorAlvoBrl,
    int PrazoMaximoDias,
    DateOnly DataAbertura,
    DateOnly DataPtaxReferencia,
    decimal PtaxUsadaUsdBrl,
    string Status,
    Guid? PropostaAceitaId,
    Guid? ContratoGeradoId,
    string? AceitaPor,
    DateTimeOffset? DataAceitacao,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid> BancosAlvo,
    IReadOnlyList<PropostaDto> Propostas)
{
    public static CotacaoDto From(Cotacao c)
    {
        List<PropostaDto> propostas = new(c.Propostas.Count);
        foreach (Proposta p in c.Propostas)
        {
            propostas.Add(PropostaDto.From(p));
        }

        return new CotacaoDto(
            c.Id,
            c.CodigoInterno,
            c.Modalidade.ToString(),
            c.ValorAlvoBrl.Valor,
            c.PrazoMaximoDias,
            new DateOnly(c.DataAbertura.Year, c.DataAbertura.Month, c.DataAbertura.Day),
            new DateOnly(c.DataPtaxReferencia.Year, c.DataPtaxReferencia.Month, c.DataPtaxReferencia.Day),
            c.PtaxUsadaUsdBrl,
            c.Status.ToString(),
            c.PropostaAceitaId,
            c.ContratoGeradoId,
            c.AceitaPor,
            c.DataAceitacao?.ToDateTimeOffset(),
            c.Observacoes,
            c.CreatedAt.ToDateTimeOffset(),
            c.UpdatedAt.ToDateTimeOffset(),
            c.BancosAlvo.ToList().AsReadOnly(),
            propostas.AsReadOnly());
    }
}
