using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Projeção de leitura de <see cref="GarantiaExigidaItem"/> para a camada de API.
/// Valores monetários são expostos como <c>decimal</c> em BRL — conversão já feita aqui.
/// </summary>
public sealed record GarantiaExigidaItemDto(
    Guid Id,
    /// <summary>Nome textual do enum <c>TipoGarantia</c> (ex: "CdbCativo", "Aval").</summary>
    string Tipo,
    decimal? PercentualSobreLimite,
    decimal? ValorFixoBrl,
    bool Obrigatoria,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? GrupoAlternativaId = null,
    string? GrupoRotulo = null)
{
    /// <summary>Constrói o DTO a partir da entidade de domínio.</summary>
    public static GarantiaExigidaItemDto From(GarantiaExigidaItem g) => new(
        g.Id,
        g.Tipo.ToString(),
        g.PercentualSobreLimite,
        g.ValorFixoBrl?.Valor,
        g.Obrigatoria,
        g.Observacoes,
        g.CreatedAt.ToDateTimeOffset(),
        g.UpdatedAt.ToDateTimeOffset(),
        g.GrupoAlternativaId,
        g.GrupoRotulo);
}
