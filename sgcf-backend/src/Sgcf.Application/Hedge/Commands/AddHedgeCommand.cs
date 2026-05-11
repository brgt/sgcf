using MediatR;
using NodaTime;

namespace Sgcf.Application.Hedge.Commands;

public sealed record AddHedgeCommand(
    Guid ContratoId,
    string Tipo,
    Guid ContraparteId,
    decimal NotionalMoedaOriginal,
    string MoedaBase,
    LocalDate DataContratacao,
    LocalDate DataVencimento,
    decimal? StrikeForward,
    decimal? StrikePut,
    decimal? StrikeCall) : IRequest<HedgeDto>;
