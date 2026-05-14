using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Alertas;

/// <summary>
/// Alerta de exposição de crédito elevada com um banco.
/// Gerado quando a soma dos saldos principais ativos atinge >= 80% do limite de crédito BRL do banco.
/// Idempotência garantida por (banco_id, data_alerta).
/// </summary>
public sealed class AlertaExposicaoBanco : Entity
{
    public Guid BancoId { get; private set; }
    public LocalDate DataAlerta { get; private set; }

    internal decimal ExposicaoBrlDecimal { get; private set; }
    internal decimal LimiteBrlDecimal { get; private set; }

    /// <summary>Soma dos saldos principais de contratos ativos vinculados ao banco, em BRL.</summary>
    public Money ExposicaoBrl => new(ExposicaoBrlDecimal, Moeda.Brl);

    /// <summary>Limite de crédito configurado no banco, em BRL.</summary>
    public Money LimiteBrl => new(LimiteBrlDecimal, Moeda.Brl);

    /// <summary>
    /// Percentual de ocupação do limite (0..1). Ex.: 0.85 significa 85%.
    /// Arredondado a 6 casas decimais (HalfUp).
    /// </summary>
    public decimal PercentualOcupacao { get; private set; }

    public Instant CriadoEm { get; private set; }

    private AlertaExposicaoBanco() { }

    /// <summary>
    /// Cria um novo alerta de exposição de banco.
    /// </summary>
    /// <param name="bancoId">Identificador do banco.</param>
    /// <param name="dataAlerta">Data do dia em que o alerta foi gerado (BRT).</param>
    /// <param name="exposicaoBrl">Total de principal ativo vinculado ao banco em BRL.</param>
    /// <param name="limiteBrl">Limite de crédito BRL configurado no banco.</param>
    /// <param name="clock">Relógio injetado — nunca DateTime.Now.</param>
    public static AlertaExposicaoBanco Criar(
        Guid bancoId,
        LocalDate dataAlerta,
        Money exposicaoBrl,
        Money limiteBrl,
        IClock clock)
    {
        if (bancoId == Guid.Empty)
        {
            throw new ArgumentException("BancoId não pode ser vazio.", nameof(bancoId));
        }

        if (exposicaoBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ExposicaoBrl deve estar em BRL.", nameof(exposicaoBrl));
        }

        if (limiteBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("LimiteBrl deve estar em BRL.", nameof(limiteBrl));
        }

        if (limiteBrl.Valor <= 0)
        {
            throw new ArgumentException("LimiteBrl deve ser positivo.", nameof(limiteBrl));
        }

        decimal percentual = Math.Round(
            exposicaoBrl.Valor / limiteBrl.Valor,
            6, MidpointRounding.AwayFromZero);

        return new AlertaExposicaoBanco
        {
            BancoId = bancoId,
            DataAlerta = dataAlerta,
            ExposicaoBrlDecimal = Math.Round(exposicaoBrl.Valor, 6, MidpointRounding.AwayFromZero),
            LimiteBrlDecimal = Math.Round(limiteBrl.Valor, 6, MidpointRounding.AwayFromZero),
            PercentualOcupacao = percentual,
            CriadoEm = clock.GetCurrentInstant()
        };
    }
}
