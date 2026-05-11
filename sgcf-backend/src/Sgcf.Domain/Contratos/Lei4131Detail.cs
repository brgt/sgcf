using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de contrato Lei 4.131/62 — empréstimo direto do exterior.
/// Tabela de extensão 1:1 com <see cref="Contrato"/> (mesma convenção de FinimpDetail).
/// </summary>
public sealed class Lei4131Detail : Entity
{
    public Guid ContratoId { get; private set; }

    /// <summary>Número do SBLC (Stand-By Letter of Credit), quando houver garantia bancária.</summary>
    public string? SblcNumero { get; private set; }

    /// <summary>Nome do banco emissor do SBLC.</summary>
    public string? SblcBancoEmissor { get; private set; }

    /// <summary>Valor de face do SBLC em USD (informativo, não monetarizado via Money).</summary>
    public decimal? SblcValorUsd { get; private set; }

    /// <summary>Indica se o contrato possui cláusula de market flex.</summary>
    public bool TemMarketFlex { get; private set; }

    /// <summary>
    /// Break funding fee armazenado como fração (0..1).
    /// Entrada em percentual humano (ex: 1.5) é dividida por 100 no factory.
    /// </summary>
    public decimal? BreakFundingFeePercentual { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private Lei4131Detail() { }

    /// <summary>
    /// Cria um novo <see cref="Lei4131Detail"/> com os dados opcionais fornecidos.
    /// <paramref name="breakFundingFeePercentual"/> é fornecido como percentual humano (ex: 1.5 para 1,5%)
    /// e convertido para fração internamente.
    /// </summary>
    public static Lei4131Detail Criar(
        Guid contratoId,
        string? sblcNumero,
        string? sblcBancoEmissor,
        decimal? sblcValorUsd,
        bool temMarketFlex,
        decimal? breakFundingFeePercentual,
        IClock clock)
    {
        Instant now = clock.GetCurrentInstant();
        return new Lei4131Detail
        {
            ContratoId = contratoId,
            SblcNumero = sblcNumero,
            SblcBancoEmissor = sblcBancoEmissor,
            SblcValorUsd = sblcValorUsd,
            TemMarketFlex = temMarketFlex,
            BreakFundingFeePercentual = breakFundingFeePercentual.HasValue
                ? breakFundingFeePercentual.Value / 100m
                : (decimal?)null,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
