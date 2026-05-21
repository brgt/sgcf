using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Hedge;

/// <summary>
/// Snapshot diário do MtM (Mark-to-Market) de um instrumento de hedge.
/// Um registro por (tenant, hedge, data) — a chave única garante idempotência no upsert.
/// </summary>
public sealed class HistoricoMtmDiario : Entity, ITenantScoped
{
    /// <summary>Tenant ao qual este registro pertence. Preenchido pelo TenantSaveInterceptor.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>Instrumento de hedge ao qual este snapshot pertence.</summary>
    public Guid HedgeId { get; private set; }

    /// <summary>Data calendário que este snapshot representa (fuso BRT).</summary>
    public LocalDate DataReferencia { get; private set; }

    /// <summary>Backing field do payoff em BRL (armazenado diretamente no banco).</summary>
    internal decimal PayoffBrlDecimal { get; private set; }

    /// <summary>Payoff em BRL com o tipo Money para operações de domínio.</summary>
    public Money PayoffBrl => new(PayoffBrlDecimal, Moeda.Brl);

    /// <summary>Taxa de câmbio spot utilizada no cálculo do MtM.</summary>
    public decimal SpotUtilizado { get; private set; }

    /// <summary>Tipo da cotação usada: "SPOT_INTRADAY" ou "PTAX_D1" (máx. 30 chars).</summary>
    public string TipoCotacao { get; private set; } = string.Empty;

    /// <summary>Instante UTC em que o registro foi persistido.</summary>
    public Instant RegistradoEm { get; private set; }

    // Construtor privado para o EF Core — nunca instanciar diretamente.
    private HistoricoMtmDiario() { }

    /// <summary>
    /// Cria um novo snapshot de MtM diário.
    /// Lança <see cref="ArgumentException"/> quando qualquer parâmetro violar as regras de negócio.
    /// </summary>
    public static HistoricoMtmDiario Criar(
        Guid hedgeId,
        LocalDate data,
        decimal payoffBrl,
        decimal spot,
        string tipoCotacao,
        Instant registradoEm)
    {
        if (hedgeId == Guid.Empty)
        {
            throw new ArgumentException("HedgeId não pode ser vazio.", nameof(hedgeId));
        }

        if (spot <= 0)
        {
            throw new ArgumentException("SpotUtilizado deve ser positivo.", nameof(spot));
        }

        if (string.IsNullOrWhiteSpace(tipoCotacao))
        {
            throw new ArgumentException("TipoCotacao não pode ser vazio.", nameof(tipoCotacao));
        }

        if (tipoCotacao.Length > 30)
        {
            throw new ArgumentException("TipoCotacao não pode exceder 30 caracteres.", nameof(tipoCotacao));
        }

        return new HistoricoMtmDiario
        {
            HedgeId        = hedgeId,
            DataReferencia = data,
            PayoffBrlDecimal = payoffBrl,
            SpotUtilizado  = spot,
            TipoCotacao    = tipoCotacao,
            RegistradoEm   = registradoEm
        };
    }

    /// <summary>
    /// Atualiza os valores do snapshot existente (caminho de upsert).
    /// </summary>
    public void Atualizar(decimal novoPayoffBrl, decimal novoSpot, string novoTipoCotacao, Instant agora)
    {
        if (novoSpot <= 0)
        {
            throw new ArgumentException("SpotUtilizado deve ser positivo.", nameof(novoSpot));
        }

        if (string.IsNullOrWhiteSpace(novoTipoCotacao))
        {
            throw new ArgumentException("TipoCotacao não pode ser vazio.", nameof(novoTipoCotacao));
        }

        if (novoTipoCotacao.Length > 30)
        {
            throw new ArgumentException("TipoCotacao não pode exceder 30 caracteres.", nameof(novoTipoCotacao));
        }

        PayoffBrlDecimal = novoPayoffBrl;
        SpotUtilizado    = novoSpot;
        TipoCotacao      = novoTipoCotacao;
        RegistradoEm     = agora;
    }
}
