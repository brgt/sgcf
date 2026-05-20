using NodaTime;

using Sgcf.Application.Common;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Simulacao;

namespace Sgcf.Application.Simulacao.Dtos;

/// <summary>
/// DTO de saída que representa uma simulação de contratação dentro de um cenário.
/// Inclui todos os campos da entidade mais o Version para invalidação de cache (AD-3).
/// </summary>
public sealed record SimulacaoContratacaoDto(
    Guid Id,
    Guid CenarioId,
    Guid BancoId,
    string Modalidade,
    string Moeda,
    decimal ValorPrincipal,
    DateOnly DataContratacaoPrevista,
    DateOnly DataPrimeiroVencimento,
    string TipoTaxa,
    decimal? TaxaAa,
    decimal? SpreadAa,
    string BaseCalculo,
    string EstruturaAmortizacao,
    string Periodicidade,
    int QuantidadeParcelas,
    string AnchorDiaMes,
    int? AnchorDiaFixo,
    string? GarantiaExigidaPrevista,
    string? Observacoes,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Projeta a entidade de domínio para este DTO.</summary>
    public static SimulacaoContratacaoDto From(SimulacaoContratacao s) =>
        new(
            s.Id,
            s.CenarioId,
            s.BancoId,
            s.Modalidade.ToString(),
            s.Moeda.ToString(),
            DecimalArredondamento.Mostrar(s.ValorPrincipal.Valor),
            ToDateOnly(s.DataContratacaoPrevista),
            ToDateOnly(s.DataPrimeiroVencimento),
            s.TipoTaxa.ToString(),
            s.TaxaAa?.AsDecimal,
            s.SpreadAa?.AsDecimal,
            s.BaseCalculo.ToString(),
            s.EstruturaAmortizacao.ToString(),
            s.Periodicidade.ToString(),
            s.QuantidadeParcelas,
            s.AnchorDiaMes.ToString(),
            s.AnchorDiaFixo,
            s.GarantiaExigidaPrevista,
            s.Observacoes,
            s.Version,
            s.CreatedAt.ToDateTimeOffset(),
            s.UpdatedAt.ToDateTimeOffset());

    private static DateOnly ToDateOnly(LocalDate d) => new(d.Year, d.Month, d.Day);
}
