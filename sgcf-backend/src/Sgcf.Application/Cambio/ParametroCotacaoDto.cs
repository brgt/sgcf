using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio;

public sealed record ParametroCotacaoDto(
    Guid Id,
    Guid? BancoId,
    string? Modalidade,
    string TipoCotacao,
    bool Ativo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static ParametroCotacaoDto From(ParametroCotacao p) =>
        new(
            p.Id,
            p.BancoId,
            p.Modalidade?.ToString(),
            p.TipoCotacao.ToString(),
            p.Ativo,
            p.CreatedAt.ToDateTimeOffset(),
            p.UpdatedAt.ToDateTimeOffset());
}
