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
    decimal? ValorApurado)
{
    public static CovenantDto From(Covenant c) =>
        new(c.Id, c.ContratoId, c.Descricao, c.Tipo, c.Status,
            c.PeriodicidadeVerificacaoMeses,
            c.ProximaVerificacaoEm?.ToString("yyyy-MM-dd", null),
            c.UltimaVerificacaoEm?.ToString("yyyy-MM-dd", null),
            c.ObservacaoVerificacao, c.LimiteNumerico, c.ValorApurado);
}
