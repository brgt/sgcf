using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Bancos;

public sealed class Banco : Entity, IAuditable
{
    public string CodigoCompe { get; private set; } = default!;
    public string RazaoSocial { get; private set; } = default!;
    public string Apelido { get; private set; } = default!;

    public bool AceitaLiquidacaoTotal { get; private set; }
    public bool AceitaLiquidacaoParcial { get; private set; }
    public bool ExigeAnuenciaExpressa { get; private set; }
    public bool ExigeParcelaInteira { get; private set; }
    public bool AceitaRefinimp { get; private set; }
    public int AvisoPrevioMinDiasUteis { get; private set; }

    internal decimal? ValorMinimoParcialPctDecimal { get; private set; }
    public Percentual? ValorMinimoParcialPct =>
        ValorMinimoParcialPctDecimal.HasValue ? Percentual.DeFracao(ValorMinimoParcialPctDecimal.Value) : null;

    public PadraoAntecipacao PadraoAntecipacao { get; private set; }

    internal decimal? BreakFundingFeePctDecimal { get; private set; }
    public Percentual? BreakFundingFeePct =>
        BreakFundingFeePctDecimal.HasValue ? Percentual.DeFracao(BreakFundingFeePctDecimal.Value) : null;

    internal decimal? TlaPctSobreSaldoDecimal { get; private set; }
    public Percentual? TlaPctSobreSaldo =>
        TlaPctSobreSaldoDecimal.HasValue ? Percentual.DeFracao(TlaPctSobreSaldoDecimal.Value) : null;

    internal decimal? TlaPctPorMesRemanescenteDecimal { get; private set; }
    public Percentual? TlaPctPorMesRemanescente =>
        TlaPctPorMesRemanescenteDecimal.HasValue ? Percentual.DeFracao(TlaPctPorMesRemanescenteDecimal.Value) : null;

    public string? ObservacoesAntecipacao { get; private set; }

    internal decimal? LimiteCreditoBrlDecimal { get; private set; }

    /// <summary>
    /// Limite de crédito em BRL para cálculo de exposição.
    /// Nulo quando não configurado (sem monitoramento de exposição para este banco).
    /// </summary>
    public Money? LimiteCreditoBrl =>
        LimiteCreditoBrlDecimal.HasValue ? new(LimiteCreditoBrlDecimal.Value, Moeda.Brl) : null;

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private Banco() { }

    public static Banco Criar(
        string codigoCompe,
        string razaoSocial,
        string apelido,
        PadraoAntecipacao padraoAntecipacao,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(codigoCompe) || codigoCompe.Length != 3)
        {
            throw new ArgumentException("CodigoCompe deve ter exatamente 3 caracteres.", nameof(codigoCompe));
        }

        if (string.IsNullOrWhiteSpace(razaoSocial))
        {
            throw new ArgumentException("RazaoSocial não pode ser vazia.", nameof(razaoSocial));
        }

        if (string.IsNullOrWhiteSpace(apelido))
        {
            throw new ArgumentException("Apelido não pode ser vazio.", nameof(apelido));
        }

        var now = clock.GetCurrentInstant();
        return new Banco
        {
            CodigoCompe = codigoCompe.ToUpperInvariant(),
            RazaoSocial = razaoSocial,
            Apelido = apelido,
            PadraoAntecipacao = padraoAntecipacao,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Habilita ou desabilita o aceite de contratos REFINIMP para este banco.
    /// </summary>
    public void AtualizarAceitaRefinimp(bool aceitaRefinimp, IClock clock)
    {
        AceitaRefinimp = aceitaRefinimp;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Atualiza o limite de crédito em BRL para este banco.
    /// Passe <c>null</c> para desabilitar o monitoramento de exposição.
    /// </summary>
    public void AtualizarLimiteCredito(decimal? limiteBrl, IClock clock)
    {
        if (limiteBrl.HasValue && limiteBrl.Value <= 0)
        {
            throw new ArgumentException("LimiteCreditoBrl deve ser positivo quando informado.", nameof(limiteBrl));
        }

        LimiteCreditoBrlDecimal = limiteBrl.HasValue
            ? Math.Round(limiteBrl.Value, 6, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        UpdatedAt = clock.GetCurrentInstant();
    }

    public void AtualizarConfigAntecipacao(
        bool aceitaLiquidacaoTotal,
        bool aceitaLiquidacaoParcial,
        bool exigeAnuenciaExpressa,
        bool exigeParcelaInteira,
        int avisoPrevioMinDiasUteis,
        decimal? valorMinimoParcialPct,
        PadraoAntecipacao padraoAntecipacao,
        decimal? breakFundingFeePct,
        decimal? tlaPctSobreSaldo,
        decimal? tlaPctPorMesRemanescente,
        string? observacoesAntecipacao,
        IClock clock)
    {
        AceitaLiquidacaoTotal = aceitaLiquidacaoTotal;
        AceitaLiquidacaoParcial = aceitaLiquidacaoParcial;
        ExigeAnuenciaExpressa = exigeAnuenciaExpressa;
        ExigeParcelaInteira = exigeParcelaInteira;
        AvisoPrevioMinDiasUteis = avisoPrevioMinDiasUteis;
        ValorMinimoParcialPctDecimal = valorMinimoParcialPct;
        PadraoAntecipacao = padraoAntecipacao;
        BreakFundingFeePctDecimal = breakFundingFeePct;
        TlaPctSobreSaldoDecimal = tlaPctSobreSaldo;
        TlaPctPorMesRemanescenteDecimal = tlaPctPorMesRemanescente;
        ObservacoesAntecipacao = observacoesAntecipacao;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
