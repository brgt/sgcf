using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

public sealed record PlanoContasModeloDto(
    Guid Id,
    string CodigoGerencial,
    string Nome,
    string Natureza,
    string? CodigoSapB1,
    DateTimeOffset UpdatedAt)
{
    public static PlanoContasModeloDto From(PlanoContasModelo modelo) =>
        new(
            modelo.Id,
            modelo.CodigoGerencial,
            modelo.Nome,
            modelo.Natureza.ToString(),
            modelo.CodigoSapB1,
            modelo.UpdatedAt.ToDateTimeOffset());
}
