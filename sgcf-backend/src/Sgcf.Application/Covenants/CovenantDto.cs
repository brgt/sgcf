using Sgcf.Domain.Covenants;

namespace Sgcf.Application.Covenants;

public sealed record CovenantDto(
    Guid Id,
    Guid ContratoId,
    string Descricao,
    TipoCovenant Tipo,
    StatusCovenant Status,
    int PeriodicidadeVerificacaoMeses,
    string? ProximaVerificacaoEm,
    string? UltimaVerificacaoEm,
    string? ObservacaoVerificacao,
    decimal? LimiteNumerico,
    decimal? ValorApurado);
