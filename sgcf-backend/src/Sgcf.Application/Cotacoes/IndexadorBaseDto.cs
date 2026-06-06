using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Representação de API do indexador base. <see cref="Tipo"/> serializa como string
/// (convenção do projeto para enums em DTOs). SPEC S40 §2.4.
/// </summary>
public sealed record IndexadorBaseDto(
    string? Tipo,
    decimal? PercentualCdi,
    decimal? SpreadAa,
    decimal? TaxaPrefixadaAa)
{
    public static IndexadorBaseDto? From(IndexadorBase? indexador) =>
        indexador is null
            ? null
            : new IndexadorBaseDto(
                indexador.Tipo?.ToString(),
                indexador.PercentualCdi,
                indexador.SpreadAa,
                indexador.TaxaPrefixadaAa);
}
