using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// DTO de leitura para um <see cref="EventoFluxoCaixa"/> individual.
/// Expõe moeda e valor na moeda original do evento — conversão para BRL fica no query handler.
/// </summary>
public sealed record EventoFluxoCaixaDto(
    Guid Id,
    string Data,
    string Tipo,
    decimal Valor,
    string Moeda,
    string Descricao,
    string RegistradoPor,
    string RegistradoEm)
{
    /// <summary>Projeta um <see cref="EventoFluxoCaixa"/> para este DTO.</summary>
    public static EventoFluxoCaixaDto From(EventoFluxoCaixa e) => new(
        e.Id,
        e.Data.ToString("yyyy-MM-dd", null),
        e.Tipo.ToString(),
        e.Valor.Valor,
        e.Valor.Moeda.ToString(),
        e.Descricao,
        e.RegistradoPor,
        e.RegistradoEm.ToString());
}
