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

    /// <summary>
    /// Atualiza as políticas institucionais de antecipação do banco.
    /// Parâmetros específicos por modalidade (PadraoAntecipacao, TLA, BreakFundingFee, etc.)
    /// residem em <see cref="Sgcf.Domain.Cotacoes.LimiteBanco"/>.
    /// </summary>
    public void AtualizarConfigAntecipacao(
        bool aceitaLiquidacaoTotal,
        bool aceitaLiquidacaoParcial,
        bool exigeAnuenciaExpressa,
        bool exigeParcelaInteira,
        int avisoPrevioMinDiasUteis,
        IClock clock)
    {
        AceitaLiquidacaoTotal = aceitaLiquidacaoTotal;
        AceitaLiquidacaoParcial = aceitaLiquidacaoParcial;
        ExigeAnuenciaExpressa = exigeAnuenciaExpressa;
        ExigeParcelaInteira = exigeParcelaInteira;
        AvisoPrevioMinDiasUteis = avisoPrevioMinDiasUteis;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
