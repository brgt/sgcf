using Sgcf.Domain.Common;

namespace Sgcf.Domain.Contratos;

/// <summary>
/// Detalhe de garantia do tipo SBLC (Standby Letter of Credit) — extensão 1:1 com <see cref="Garantia"/>.
/// </summary>
public sealed class GarantiaSblcDetail : Entity
{
    public Guid GarantiaId { get; private set; }
    public string BancoEmissor { get; private set; } = default!;
    public string PaisEmissor { get; private set; } = default!;
    public string? SwiftCode { get; private set; }
    public int ValidadeDias { get; private set; }

    /// <summary>Comissão anual do SBLC armazenada como fração (0..1). Null quando não informada.</summary>
    internal decimal? ComissaoAaDecimal { get; private set; }

    /// <summary>Comissão anual do SBLC como <see cref="Percentual"/> tipado. Null quando não informada.</summary>
    public Percentual? ComissaoAa =>
        ComissaoAaDecimal.HasValue ? Percentual.DeFracao(ComissaoAaDecimal.Value) : null;

    public string? NumeroSblc { get; private set; }

    private GarantiaSblcDetail() { }

    /// <summary>
    /// Cria um novo detalhe SBLC.
    /// <paramref name="comissaoAaPct"/> é fornecido como valor humano (ex: 1.5 para 1,5% a.a.).
    /// </summary>
    public static GarantiaSblcDetail Criar(
        Guid garantiaId,
        string bancoEmissor,
        string paisEmissor,
        string? swiftCode,
        int validadeDias,
        decimal? comissaoAaPct,
        string? numeroSblc)
    {
        if (string.IsNullOrWhiteSpace(bancoEmissor))
        {
            throw new ArgumentException("BancoEmissor não pode ser vazio.", nameof(bancoEmissor));
        }

        if (string.IsNullOrWhiteSpace(paisEmissor))
        {
            throw new ArgumentException("PaisEmissor não pode ser vazio.", nameof(paisEmissor));
        }

        if (validadeDias <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(validadeDias), "ValidadeDias deve ser maior que zero.");
        }

        return new GarantiaSblcDetail
        {
            GarantiaId = garantiaId,
            BancoEmissor = bancoEmissor,
            PaisEmissor = paisEmissor,
            SwiftCode = swiftCode,
            ValidadeDias = validadeDias,
            ComissaoAaDecimal = comissaoAaPct.HasValue ? comissaoAaPct.Value / 100m : (decimal?)null,
            NumeroSblc = numeroSblc
        };
    }
}
