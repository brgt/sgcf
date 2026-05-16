using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Limite operacional de um banco para uma modalidade.
/// Aggregate independente de Cotacao. Controla o teto de exposição permitido.
/// SPEC §3.1.
/// </summary>
public sealed class LimiteBanco : Entity, IAuditable
{
    public Guid BancoId { get; private set; }
    public ModalidadeContrato Modalidade { get; private set; }

    internal decimal ValorLimiteBrlDecimal { get; private set; }
    public Money ValorLimiteBrl => new(ValorLimiteBrlDecimal, Moeda.Brl);

    internal decimal ValorUtilizadoBrlDecimal { get; private set; }
    public Money ValorUtilizadoBrl => new(ValorUtilizadoBrlDecimal, Moeda.Brl);

    /// <summary>Disponível = Limite − Utilizado. Propriedade computada, nunca persista diretamente.</summary>
    public Money ValorDisponivelBrl =>
        new(Math.Max(0m, ValorLimiteBrlDecimal - ValorUtilizadoBrlDecimal), Moeda.Brl);

    public LocalDate DataVigenciaInicio { get; private set; }
    public LocalDate? DataVigenciaFim { get; private set; }
    public string? Observacoes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private LimiteBanco() { }

    /// <summary>
    /// Cria novo limite operacional para banco/modalidade.
    /// Invariante: ValorUtilizado inicial é zero; ValorLimite deve ser positivo.
    /// </summary>
    public static LimiteBanco Criar(
        Guid bancoId,
        ModalidadeContrato modalidade,
        Money valorLimiteBrl,
        LocalDate dataVigenciaInicio,
        IClock clock,
        LocalDate? dataVigenciaFim = null,
        string? observacoes = null)
    {
        if (valorLimiteBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorLimiteBrl deve ser em BRL.", nameof(valorLimiteBrl));
        }

        if (valorLimiteBrl.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valorLimiteBrl), "ValorLimiteBrl deve ser positivo.");
        }

        if (dataVigenciaFim.HasValue && dataVigenciaFim.Value <= dataVigenciaInicio)
        {
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(dataVigenciaFim));
        }

        var now = clock.GetCurrentInstant();
        return new LimiteBanco
        {
            BancoId = bancoId,
            Modalidade = modalidade,
            ValorLimiteBrlDecimal = valorLimiteBrl.Valor,
            ValorUtilizadoBrlDecimal = 0m,
            DataVigenciaInicio = dataVigenciaInicio,
            DataVigenciaFim = dataVigenciaFim,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Atualiza campos do limite (valor e datas de vigência).
    /// Invariante: novo limite não pode ser menor que valor já utilizado.
    /// </summary>
    public void Atualizar(
        IClock clock,
        Money? novoLimiteBrl = null,
        LocalDate? novaDataVigenciaInicio = null,
        LocalDate? novaDataVigenciaFim = null,
        string? observacoes = null)
    {
        if (novoLimiteBrl.HasValue)
        {
            if (novoLimiteBrl.Value.Moeda != Moeda.Brl)
            {
                throw new ArgumentException("NovoLimiteBrl deve ser em BRL.", nameof(novoLimiteBrl));
            }

            if (novoLimiteBrl.Value.Valor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(novoLimiteBrl), "NovoLimiteBrl deve ser positivo.");
            }

            if (novoLimiteBrl.Value.Valor < ValorUtilizadoBrlDecimal)
            {
                throw new InvalidOperationException(
                    $"Novo limite (BRL {novoLimiteBrl.Value.Valor:F2}) é menor que o valor já utilizado (BRL {ValorUtilizadoBrlDecimal:F2}).");
            }

            ValorLimiteBrlDecimal = novoLimiteBrl.Value.Valor;
        }

        LocalDate vigenciaInicio = novaDataVigenciaInicio ?? DataVigenciaInicio;
        LocalDate? vigenciaFim = novaDataVigenciaFim ?? DataVigenciaFim;

        if (vigenciaFim.HasValue && vigenciaFim.Value <= vigenciaInicio)
        {
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(novaDataVigenciaFim));
        }

        if (novaDataVigenciaInicio.HasValue)
        {
            DataVigenciaInicio = novaDataVigenciaInicio.Value;
        }

        if (novaDataVigenciaFim.HasValue)
        {
            DataVigenciaFim = novaDataVigenciaFim;
        }

        if (observacoes is not null)
        {
            Observacoes = observacoes;
        }

        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Incrementa o valor utilizado ao confirmar uso do limite (ex: conversão em contrato).
    /// Invariante: ValorUtilizado ≤ ValorLimite.
    /// </summary>
    public void RegistrarUso(Money valor, IClock clock)
    {
        if (valor.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("Valor deve ser em BRL.", nameof(valor));
        }

        if (valor.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor de uso deve ser positivo.");
        }

        decimal novoUtilizado = ValorUtilizadoBrlDecimal + valor.Valor;
        if (novoUtilizado > ValorLimiteBrlDecimal)
        {
            throw new InvalidOperationException(
                $"Uso de BRL {valor.Valor:F2} excederia o limite de BRL {ValorLimiteBrlDecimal:F2} (utilizado atual: BRL {ValorUtilizadoBrlDecimal:F2}).");
        }

        ValorUtilizadoBrlDecimal = novoUtilizado;
        UpdatedAt = clock.GetCurrentInstant();
    }

    /// <summary>
    /// Decrementa o valor utilizado ao liberar uso (ex: liquidação ou cancelamento de contrato).
    /// Não permite valor utilizado negativo.
    /// </summary>
    public void LiberarUso(Money valor, IClock clock)
    {
        if (valor.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("Valor deve ser em BRL.", nameof(valor));
        }

        if (valor.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor de liberação deve ser positivo.");
        }

        decimal novoUtilizado = ValorUtilizadoBrlDecimal - valor.Valor;
        if (novoUtilizado < 0)
        {
            throw new InvalidOperationException(
                $"Liberação de BRL {valor.Valor:F2} resultaria em valor utilizado negativo (utilizado atual: BRL {ValorUtilizadoBrlDecimal:F2}).");
        }

        ValorUtilizadoBrlDecimal = novoUtilizado;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
