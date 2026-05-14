using Sgcf.Domain.Calendario;

namespace Sgcf.Application.Calendario;

/// <summary>
/// Representação de leitura de um <see cref="Feriado"/> para a camada de API.
/// Datas expostas como <see cref="DateOnly"/> e enums como string para
/// melhor interoperabilidade com clientes não .NET.
/// </summary>
public sealed record FeriadoDto(
    Guid Id,
    DateOnly Data,
    string Tipo,
    string Escopo,
    string Descricao,
    string Fonte,
    int AnoReferencia)
{
    public static FeriadoDto From(Feriado feriado) =>
        new(
            feriado.Id,
            new DateOnly(feriado.Data.Year, feriado.Data.Month, feriado.Data.Day),
            feriado.Tipo.ToString(),
            feriado.Escopo.ToString(),
            feriado.Descricao,
            feriado.Fonte.ToString(),
            feriado.AnoReferencia);
}
