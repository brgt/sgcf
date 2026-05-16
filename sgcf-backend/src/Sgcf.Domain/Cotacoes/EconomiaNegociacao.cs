using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Snapshot imutável criado no momento da conversão da Cotacao em Contrato.
/// Registra a economia (ou perda) obtida na negociação, equalizada por CDI quando prazos diferem.
/// SPEC §3.1 — "Nunca alterar EconomiaNegociacao após criação (snapshot imutável)."
/// </summary>
public sealed class EconomiaNegociacao : Entity
{
    public Guid CotacaoId { get; private set; }
    public Guid ContratoId { get; private set; }

    /// <summary>JSON imutável da proposta aceita no momento da conversão.</summary>
    public string SnapshotPropostaJson { get; private set; } = default!;

    /// <summary>JSON imutável do contrato gerado no momento da conversão.</summary>
    public string SnapshotContratoJson { get; private set; } = default!;

    public decimal CetPropostaAaPercentual { get; private set; }
    public decimal CetContratoAaPercentual { get; private set; }

    internal decimal EconomiaBrlDecimal { get; private set; }

    /// <summary>
    /// Diferença bruta (positivo = economia, negativo = perda).
    /// Fórmula: (CET_proposta - CET_contrato) × ValorPrincipal × (Prazo/360) — SPEC §5.2.
    /// </summary>
    public Money EconomiaBrl => new(EconomiaBrlDecimal, Moeda.Brl);

    internal decimal EconomiaAjustadaCdiBrlDecimal { get; private set; }

    /// <summary>Economia equalizada por CDI quando os prazos diferem. Método VPL — SPEC §5.2.</summary>
    public Money EconomiaAjustadaCdiBrl => new(EconomiaAjustadaCdiBrlDecimal, Moeda.Brl);

    public LocalDate DataReferenciaCdi { get; private set; }
    public Instant CreatedAt { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private EconomiaNegociacao() { }

    /// <summary>
    /// Factory. Cria snapshot imutável. Após criação, nenhuma propriedade pode ser alterada
    /// (setters private, sem métodos mutadores públicos).
    /// </summary>
    public static EconomiaNegociacao Criar(
        Guid cotacaoId,
        Guid contratoId,
        string snapshotPropostaJson,
        string snapshotContratoJson,
        decimal cetPropostaAaPercentual,
        decimal cetContratoAaPercentual,
        Money economiaBrl,
        Money economiaAjustadaCdiBrl,
        LocalDate dataReferenciaCdi,
        IClock clock)
    {
        if (cotacaoId == Guid.Empty)
        {
            throw new ArgumentException("CotacaoId não pode ser vazio.", nameof(cotacaoId));
        }

        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        if (string.IsNullOrWhiteSpace(snapshotPropostaJson))
        {
            throw new ArgumentException("SnapshotPropostaJson não pode ser vazio.", nameof(snapshotPropostaJson));
        }

        if (string.IsNullOrWhiteSpace(snapshotContratoJson))
        {
            throw new ArgumentException("SnapshotContratoJson não pode ser vazio.", nameof(snapshotContratoJson));
        }

        if (economiaBrl.Moeda != Moeda.Brl || economiaAjustadaCdiBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("Valores de economia devem ser em BRL.");
        }

        return new EconomiaNegociacao
        {
            CotacaoId = cotacaoId,
            ContratoId = contratoId,
            SnapshotPropostaJson = snapshotPropostaJson,
            SnapshotContratoJson = snapshotContratoJson,
            CetPropostaAaPercentual = cetPropostaAaPercentual,
            CetContratoAaPercentual = cetContratoAaPercentual,
            EconomiaBrlDecimal = economiaBrl.Valor,
            EconomiaAjustadaCdiBrlDecimal = economiaAjustadaCdiBrl.Valor,
            DataReferenciaCdi = dataReferenciaCdi,
            CreatedAt = clock.GetCurrentInstant(),
        };
    }
}
