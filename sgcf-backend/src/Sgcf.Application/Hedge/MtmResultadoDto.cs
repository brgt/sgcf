namespace Sgcf.Application.Hedge;

public sealed record MtmResultadoDto(
    Guid HedgeId,
    decimal PayoffBrl,
    string Posicao,
    decimal SpotUtilizado,
    string TipoCotacao,
    string DataHoraCotacao,
    IReadOnlyList<string> Alertas);
