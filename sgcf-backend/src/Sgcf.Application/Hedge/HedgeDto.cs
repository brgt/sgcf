namespace Sgcf.Application.Hedge;

public sealed record HedgeDto(
    Guid Id,
    Guid ContratoId,
    string Tipo,
    Guid ContraparteId,
    decimal NotionalMoedaOriginal,
    string MoedaBase,
    string DataContratacao,
    string DataVencimento,
    decimal? StrikeForward,
    decimal? StrikePut,
    decimal? StrikeCall,
    string Status,
    IReadOnlyList<string> Alertas);
