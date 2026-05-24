using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Projeção enriquecida do limite guarda-chuva vigente de um banco.
/// Além dos campos de <see cref="LimiteGlobalBancoDto"/>, expõe valores calculados em tempo de
/// consulta: utilização atual, disponível e o regime operacional do banco.
/// </summary>
/// <param name="ValorUtilizadoBrl">
/// Cenário A (GlobalPuro): soma do saldo devedor dos contratos ativos.
/// Cenário B (PerModalidade): soma de <c>LimiteBanco.ValorUtilizadoBrl</c> das modalidades vigentes.
/// </param>
/// <param name="ValorDisponivelBrl">
/// Diferença entre <see cref="ValorLimiteBrl"/> e <see cref="ValorUtilizadoBrl"/>, com piso em zero.
/// </param>
/// <param name="Regime">
/// <c>"GlobalPuro"</c> quando o banco opera apenas com o limite guarda-chuva;
/// <c>"PerModalidade"</c> quando existem linhas por modalidade coexistindo com o guarda-chuva.
/// </param>
public sealed record LimiteGlobalBancoVigenteDto(
    Guid Id,
    Guid BancoId,
    decimal ValorLimiteBrl,
    DateOnly DataVigenciaInicio,
    DateOnly? DataVigenciaFim,
    string? Observacoes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<LimiteGlobalBancoHistoricoDto> Historico,
    decimal ValorUtilizadoBrl,
    decimal ValorDisponivelBrl,
    string Regime)
{
    /// <summary>
    /// Constrói o DTO a partir do agregado de domínio e dos valores calculados pelo caller.
    /// O caller (query handler) é responsável por computar <paramref name="valorUtilizadoBrl"/>
    /// consultando <c>IConsultaSaldoBanco</c> conforme o regime detectado.
    /// </summary>
    public static LimiteGlobalBancoVigenteDto From(
        LimiteGlobalBanco l,
        decimal valorUtilizadoBrl,
        bool isPerModalidade)
    {
        List<LimiteGlobalBancoHistoricoDto> historico = new(l.Historico.Count);
        foreach (LimiteGlobalBancoHistorico h in l.Historico.OrderByDescending(h => h.RegistradoEm))
        {
            historico.Add(LimiteGlobalBancoHistoricoDto.From(h));
        }

        return new LimiteGlobalBancoVigenteDto(
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
            historico.AsReadOnly(),
            ValorUtilizadoBrl: valorUtilizadoBrl,
            ValorDisponivelBrl: Math.Max(0m, l.ValorLimiteBrl.Valor - valorUtilizadoBrl),
            Regime: isPerModalidade ? "PerModalidade" : "GlobalPuro");
    }
}
