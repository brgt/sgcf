using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Projeção de leitura de <see cref="LimiteBancoHistorico"/> para a camada de API.
/// Expõe valores monetários como <c>decimal</c> em BRL.
/// </summary>
public sealed record LimiteBancoHistoricoDto(
    Guid Id,
    Guid LimiteBancoId,
    /// <summary>Valor anterior em BRL. Null quando é a entrada de criação.</summary>
    decimal? ValorAnteriorBrl,
    decimal ValorNovoBrl,
    DateTimeOffset RegistradoEm,
    string? Observacoes)
{
    /// <summary>Constrói o DTO a partir da entidade de domínio.</summary>
    public static LimiteBancoHistoricoDto From(LimiteBancoHistorico h) => new(
        h.Id,
        h.LimiteBancoId,
        h.ValorAnteriorBrl?.Valor,
        h.ValorNovoBrl.Valor,
        h.RegistradoEm.ToDateTimeOffset(),
        h.Observacoes);
}
