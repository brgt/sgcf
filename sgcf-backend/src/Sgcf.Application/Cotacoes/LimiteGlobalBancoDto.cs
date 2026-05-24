using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Projeção de leitura de <see cref="LimiteGlobalBancoHistorico"/> para a camada de API.
/// Expõe valores monetários como <c>decimal</c> em BRL.
/// </summary>
public sealed record LimiteGlobalBancoHistoricoDto(
    Guid Id,
    Guid LimiteGlobalBancoId,
    /// <summary>Valor anterior em BRL. Null quando é a entrada de criação do limite global.</summary>
    decimal? ValorAnteriorBrl,
    decimal ValorNovoBrl,
    DateTimeOffset RegistradoEm,
    string? Observacoes)
{
    /// <summary>Constrói o DTO a partir da entidade de domínio.</summary>
    public static LimiteGlobalBancoHistoricoDto From(LimiteGlobalBancoHistorico h) => new(
        h.Id,
        h.LimiteGlobalBancoId,
        h.ValorAnteriorBrl?.Valor,
        h.ValorNovoBrl.Valor,
        h.RegistradoEm.ToDateTimeOffset(),
        h.Observacoes);
}

/// <summary>
/// Projeção de leitura de <see cref="LimiteGlobalBanco"/> para a camada de API.
/// Expõe valores monetários como <c>decimal</c> em BRL e datas como <c>DateOnly</c>.
/// </summary>
public sealed record LimiteGlobalBancoDto(
    Guid Id,
    Guid BancoId,
    decimal ValorLimiteBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<LimiteGlobalBancoHistoricoDto> Historico)
{
    /// <summary>Constrói o DTO a partir do agregado de domínio.</summary>
    public static LimiteGlobalBancoDto From(LimiteGlobalBanco l)
    {
        List<LimiteGlobalBancoHistoricoDto> historico = new(l.Historico.Count);
        foreach (LimiteGlobalBancoHistorico h in l.Historico.OrderByDescending(h => h.RegistradoEm))
        {
            historico.Add(LimiteGlobalBancoHistoricoDto.From(h));
        }

        return new LimiteGlobalBancoDto(
            l.Id,
            l.BancoId,
            l.ValorLimiteBrl.Valor,
            new DateOnly(l.DataVigenciaInicio.Year, l.DataVigenciaInicio.Month, l.DataVigenciaInicio.Day),
            l.DataVigenciaFim.HasValue
                ? new DateOnly(l.DataVigenciaFim.Value.Year, l.DataVigenciaFim.Value.Month, l.DataVigenciaFim.Value.Day)
                : null,
            l.Observacoes,
            l.CreatedAt.ToDateTimeOffset(),
            l.UpdatedAt.ToDateTimeOffset(),
            historico.AsReadOnly());
    }
}
