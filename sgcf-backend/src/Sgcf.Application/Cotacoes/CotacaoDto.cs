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
    // Onda 0 F0.1: nullable para modalidades BRL puras (NCE, CapitalDeGiro, FGI).
    DateOnly? DataPtaxReferencia,
    decimal? PtaxUsadaUsdBrl,
    string Status,
    Guid? PropostaAceitaId,
    Guid? ContratoGeradoId,
    string? AceitaPor,
    DateTimeOffset? DataAceitacao,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid> BancosAlvo,
    IReadOnlyList<PropostaDto> Propostas,
    // Onda 1 REFINIMP: null para todas as outras modalidades.
    Guid? ContratoMaeId = null)
{
    public static CotacaoDto From(Cotacao c)
    {
        List<PropostaDto> propostas = new(c.Propostas.Count);
        foreach (Proposta p in c.Propostas)
        {
            propostas.Add(PropostaDto.From(p));
        }

        // DataPtaxReferencia é null para modalidades BRL — converter apenas quando presente.
        DateOnly? dataPtax = c.DataPtaxReferencia.HasValue
            ? new DateOnly(c.DataPtaxReferencia.Value.Year, c.DataPtaxReferencia.Value.Month, c.DataPtaxReferencia.Value.Day)
            : null;

        return new CotacaoDto(
            c.Id,
            c.CodigoInterno,
            c.Modalidade.ToString(),
            c.ValorAlvoBrl.Valor,
            c.PrazoMaximoDias,
            new DateOnly(c.DataAbertura.Year, c.DataAbertura.Month, c.DataAbertura.Day),
            dataPtax,
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
            propostas.AsReadOnly(),
            ContratoMaeId: c.ContratoMaeId);
    }
}
