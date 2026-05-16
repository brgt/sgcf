using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

public sealed record LimiteBancoDto(
    Guid Id,
    Guid BancoId,
    string Modalidade,
    decimal ValorLimiteBrl,
    decimal ValorUtilizadoBrl,
    decimal ValorDisponivelBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static LimiteBancoDto From(LimiteBanco l) => new(
        l.Id,
        l.BancoId,
        l.Modalidade.ToString(),
        l.ValorLimiteBrl.Valor,
        l.ValorUtilizadoBrl.Valor,
        l.ValorDisponivelBrl.Valor,
        new DateOnly(l.DataVigenciaInicio.Year, l.DataVigenciaInicio.Month, l.DataVigenciaInicio.Day),
        l.DataVigenciaFim.HasValue
            ? new DateOnly(l.DataVigenciaFim.Value.Year, l.DataVigenciaFim.Value.Month, l.DataVigenciaFim.Value.Day)
            : null,
        l.Observacoes,
        l.CreatedAt.ToDateTimeOffset(),
        l.UpdatedAt.ToDateTimeOffset());
}
