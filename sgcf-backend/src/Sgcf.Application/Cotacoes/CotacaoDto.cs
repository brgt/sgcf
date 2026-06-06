using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public sealed record CotacaoDto(
    Guid Id,
    string CodigoInterno,
    string Modalidade,
    decimal ValorAlvoBrl,
    // Prazo: canônico (derivado) + tenor estruturado (intenção). SPEC S40 §2.1.
    int PrazoMaximoDias,
    int PrazoMaximoValor,
    string PrazoMaximoUnidade,
    DateOnly DataAbertura,
    // S40 §2.2: moeda alvo da cotação.
    string MoedaAlvo,
    // Onda 0 F0.1: nullable para modalidades BRL puras (NCE, CapitalDeGiro, FGI).
    DateOnly? DataPtaxReferencia,
    // S40 §2.6: PtaxUsada canônico (multimoeda). PtaxUsadaUsdBrl depreciado (só USD).
    decimal? PtaxUsada,
    decimal? PtaxUsadaUsdBrl,
    // S40 §2.3–§2.5: campos de domínio opcionais.
    int? CarenciaMeses,
    IndexadorBaseDto? IndexadorBase,
    string? FinalidadeBndes,
    string? BancoRepassadorPretendido,
    decimal? PercentualCoberturaFgi,
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
    // S40 §4.6: alertas de validação suave (vazio em leitura; preenchido em POST/PATCH).
    IReadOnlyList<AlertaDto> Alertas,
    // Onda 1 REFINIMP: null para todas as outras modalidades.
    Guid? ContratoMaeId = null)
{
    public static CotacaoDto From(Cotacao c) => From(c, []);

    public static CotacaoDto From(Cotacao c, IReadOnlyList<AlertaDto> alertas)
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
            Id: c.Id,
            CodigoInterno: c.CodigoInterno,
            Modalidade: c.Modalidade.ToString(),
            ValorAlvoBrl: c.ValorAlvoBrl.Valor,
            PrazoMaximoDias: c.PrazoMaximoDias,
            PrazoMaximoValor: c.PrazoMaximoValor,
            PrazoMaximoUnidade: c.PrazoMaximoUnidade.ToString(),
            DataAbertura: new DateOnly(c.DataAbertura.Year, c.DataAbertura.Month, c.DataAbertura.Day),
            MoedaAlvo: c.MoedaAlvo.ToString(),
            DataPtaxReferencia: dataPtax,
            PtaxUsada: c.PtaxUsada,
            PtaxUsadaUsdBrl: c.PtaxUsadaUsdBrl,
            CarenciaMeses: c.CarenciaMeses,
            IndexadorBase: IndexadorBaseDto.From(c.IndexadorBase),
            FinalidadeBndes: c.FinalidadeBndes,
            BancoRepassadorPretendido: c.BancoRepassadorPretendido,
            PercentualCoberturaFgi: c.PercentualCoberturaFgi,
            Status: c.Status.ToString(),
            PropostaAceitaId: c.PropostaAceitaId,
            ContratoGeradoId: c.ContratoGeradoId,
            AceitaPor: c.AceitaPor,
            DataAceitacao: c.DataAceitacao?.ToDateTimeOffset(),
            Observacoes: c.Observacoes,
            CreatedAt: c.CreatedAt.ToDateTimeOffset(),
            UpdatedAt: c.UpdatedAt.ToDateTimeOffset(),
            BancosAlvo: c.BancosAlvo.ToList().AsReadOnly(),
            Propostas: propostas.AsReadOnly(),
            Alertas: alertas,
            ContratoMaeId: c.ContratoMaeId);
    }
}
