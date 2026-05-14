using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Alertas;

/// <summary>
/// Alerta de vencimento iminente de parcela de cronograma.
/// Gerado automaticamente pelo job diário para os horizontes D-7, D-3 e D-0.
/// Idempotência garantida por (contrato_id, tipo_alerta, data_vencimento).
/// </summary>
public sealed class AlertaVencimento : Entity
{
    public Guid ContratoId { get; private set; }

    /// <summary>"D_MENOS_7", "D_MENOS_3" ou "D_MENOS_0".</summary>
    public string TipoAlerta { get; private set; } = default!;

    public LocalDate DataVencimento { get; private set; }
    public LocalDate DataAlerta { get; private set; }

    internal decimal ValorDecimal { get; private set; }
    public Moeda Moeda { get; private set; }

    /// <summary>Valor do evento de cronograma que está vencendo.</summary>
    public Money Valor => new(ValorDecimal, Moeda);

    public Instant CriadoEm { get; private set; }

    private AlertaVencimento() { }

    /// <summary>
    /// Cria um novo alerta de vencimento.
    /// </summary>
    /// <param name="contratoId">Identificador do contrato ao qual o evento pertence.</param>
    /// <param name="tipoAlerta">Horizonte: "D_MENOS_7", "D_MENOS_3" ou "D_MENOS_0".</param>
    /// <param name="dataVencimento">Data prevista do evento de cronograma.</param>
    /// <param name="dataAlerta">Data em que o alerta foi gerado (hoje em BRT).</param>
    /// <param name="valor">Valor do evento expresso na moeda original do contrato.</param>
    /// <param name="clock">Relógio injetado — nunca DateTime.Now.</param>
    public static AlertaVencimento Criar(
        Guid contratoId,
        string tipoAlerta,
        LocalDate dataVencimento,
        LocalDate dataAlerta,
        Money valor,
        IClock clock)
    {
        if (contratoId == Guid.Empty)
        {
            throw new ArgumentException("ContratoId não pode ser vazio.", nameof(contratoId));
        }

        if (string.IsNullOrWhiteSpace(tipoAlerta))
        {
            throw new ArgumentException("TipoAlerta não pode ser vazio.", nameof(tipoAlerta));
        }

        return new AlertaVencimento
        {
            ContratoId = contratoId,
            TipoAlerta = tipoAlerta,
            DataVencimento = dataVencimento,
            DataAlerta = dataAlerta,
            ValorDecimal = Math.Round(valor.Valor, 6, MidpointRounding.AwayFromZero),
            Moeda = valor.Moeda,
            CriadoEm = clock.GetCurrentInstant()
        };
    }
}
