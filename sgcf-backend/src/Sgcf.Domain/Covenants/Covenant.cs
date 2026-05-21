using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Covenants;

/// <summary>
/// Cláusula restritiva de contrato de financiamento. GAP-CKP-13.
/// Monitora cumprimento periódico de obrigações financeiras e não-financeiras.
/// </summary>
public sealed class Covenant : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid ContratoId { get; private set; }

    /// <summary>Descrição da cláusula conforme contrato.</summary>
    public string Descricao { get; private set; } = default!;

    public TipoCovenant Tipo { get; private set; }
    public StatusCovenant Status { get; private set; }

    /// <summary>Periodicidade de verificação em meses (ex.: 3 = trimestral, 12 = anual).</summary>
    public int PeriodicidadeVerificacaoMeses { get; private set; }

    /// <summary>Data da próxima verificação obrigatória.</summary>
    public LocalDate? ProximaVerificacaoEm { get; private set; }

    /// <summary>Data da última verificação registrada.</summary>
    public LocalDate? UltimaVerificacaoEm { get; private set; }

    /// <summary>Observações sobre o resultado da última verificação.</summary>
    public string? ObservacaoVerificacao { get; private set; }

    /// <summary>Valor-limite numérico para covenants financeiros (ex.: índice de cobertura ≥ 1.2).</summary>
    public decimal? LimiteNumerico { get; private set; }

    /// <summary>Valor apurado na última verificação.</summary>
    public decimal? ValorApurado { get; private set; }

    public Instant CriadoEm { get; private set; }
    public Instant AtualizadoEm { get; private set; }

    private Covenant() { }

    public static Covenant Criar(
        Guid contratoId,
        string descricao,
        TipoCovenant tipo,
        int periodicidadeMeses,
        LocalDate? proximaVerificacaoEm,
        decimal? limiteNumerico,
        Instant agora)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descrição não pode ser vazia.", nameof(descricao));
        }

        if (periodicidadeMeses <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodicidadeMeses), "Periodicidade deve ser maior que zero.");
        }

        return new Covenant
        {
            ContratoId = contratoId,
            Descricao = descricao.Trim(),
            Tipo = tipo,
            Status = StatusCovenant.Pendente,
            PeriodicidadeVerificacaoMeses = periodicidadeMeses,
            ProximaVerificacaoEm = proximaVerificacaoEm,
            LimiteNumerico = limiteNumerico,
            CriadoEm = agora,
            AtualizadoEm = agora,
        };
    }

    public void Atualizar(
        string descricao,
        int periodicidadeMeses,
        LocalDate? proximaVerificacaoEm,
        decimal? limiteNumerico,
        Instant agora)
    {
        if (string.IsNullOrWhiteSpace(descricao))
        {
            throw new ArgumentException("Descrição não pode ser vazia.", nameof(descricao));
        }

        if (periodicidadeMeses <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodicidadeMeses), "Periodicidade deve ser maior que zero.");
        }

        Descricao = descricao.Trim();
        PeriodicidadeVerificacaoMeses = periodicidadeMeses;
        ProximaVerificacaoEm = proximaVerificacaoEm;
        LimiteNumerico = limiteNumerico;
        AtualizadoEm = agora;
    }

    public void RegistrarVerificacao(
        StatusCovenant novoStatus,
        LocalDate dataVerificacao,
        LocalDate? proximaVerificacao,
        decimal? valorApurado,
        string? observacao,
        Instant agora)
    {
        Status = novoStatus;
        UltimaVerificacaoEm = dataVerificacao;
        ProximaVerificacaoEm = proximaVerificacao;
        ValorApurado = valorApurado;
        ObservacaoVerificacao = observacao;
        AtualizadoEm = agora;
    }
}
