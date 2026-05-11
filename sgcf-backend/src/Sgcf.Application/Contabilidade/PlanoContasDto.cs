using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

public sealed record PlanoContasDto(
    Guid Id,
    string CodigoGerencial,
    string Nome,
    string Natureza,
    string? CodigoSapB1,
    bool Ativo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static PlanoContasDto From(PlanoContasGerencial conta) =>
        new(
            conta.Id,
            conta.CodigoGerencial,
            conta.Nome,
            conta.Natureza.ToString(),
            conta.CodigoSapB1,
            conta.Ativo,
            conta.CreatedAt.ToDateTimeOffset(),
            conta.UpdatedAt.ToDateTimeOffset());
}
