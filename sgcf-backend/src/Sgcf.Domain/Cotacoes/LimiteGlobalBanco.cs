using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Limite global (umbrella) que um banco concede à empresa.
/// Funciona como teto agregado independente de modalidade, coexistindo com
/// <see cref="LimiteBanco"/> (que representa limites por modalidade).
/// Dois regimes surgem: Cenário A (apenas global) e Cenário B (global + modalidades).
/// SPEC §3.2 — LimiteGlobalBanco.
/// </summary>
public sealed class LimiteGlobalBanco : Entity, IAuditable, ITenantScoped
{
    public Guid TenantId { get; private set; }
    public Guid BancoId { get; private set; }

    internal decimal ValorLimiteBrlDecimal { get; private set; }
    public Money ValorLimiteBrl => new(ValorLimiteBrlDecimal, Moeda.Brl);

    public LocalDate DataVigenciaInicio { get; private set; }
    public LocalDate? DataVigenciaFim { get; private set; }
    public string? Observacoes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    private readonly List<LimiteGlobalBancoHistorico> _historico = new();

    /// <summary>
    /// Histórico de alterações do valor do limite guarda-chuva.
    /// Cada mudança de <see cref="ValorLimiteBrl"/> registra uma entrada para análise de tendência.
    /// </summary>
    public IReadOnlyCollection<LimiteGlobalBancoHistorico> Historico => _historico.AsReadOnly();

    /// <summary>Construtor privado para EF Core.</summary>
    private LimiteGlobalBanco() { }

    /// <summary>
    /// Cria novo limite global para o banco.
    /// Invariantes: LG-01 (moeda BRL), LG-02 (valor positivo), LG-03 (vigência coerente).
    /// LG-07: grava entrada inicial no histórico com ValorAnterior = null.
    /// </summary>
    public static LimiteGlobalBanco Criar(
        Guid bancoId,
        Money valorLimiteBrl,
        LocalDate dataVigenciaInicio,
        IClock clock,
        LocalDate? dataVigenciaFim = null,
        string? observacoes = null)
    {
        // LG-01
        if (valorLimiteBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorLimiteBrl deve ser em BRL.", nameof(valorLimiteBrl));
        }

        // LG-02
        if (valorLimiteBrl.Valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valorLimiteBrl), "ValorLimiteBrl deve ser positivo.");
        }

        // LG-03
        if (dataVigenciaFim.HasValue && dataVigenciaFim.Value <= dataVigenciaInicio)
        {
            throw new ArgumentException(
                "DataVigenciaFim deve ser posterior a DataVigenciaInicio.",
                nameof(dataVigenciaFim));
        }

        var now = clock.GetCurrentInstant();
        var limite = new LimiteGlobalBanco
        {
            BancoId = bancoId,
            ValorLimiteBrlDecimal = valorLimiteBrl.Valor,
            DataVigenciaInicio = dataVigenciaInicio,
            DataVigenciaFim = dataVigenciaFim,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // LG-07: entrada inicial com ValorAnterior = null
        limite._historico.Add(LimiteGlobalBancoHistorico.Criar(
            limiteGlobalBancoId: limite.Id,
            valorAnteriorBrl: null,
            valorNovoBrl: valorLimiteBrl,
            registradoEm: now,
            observacoes: "Criação do limite global"));

        return limite;
    }

    /// <summary>
    /// Atualiza valor e/ou vigência do limite guarda-chuva.
    /// Reduções de valor exigem que <paramref name="saldoDevedorAtual"/> seja fornecido
    /// pelo caller (Application) — o domínio não conhece o repositório.
    /// LG-06: bloqueia redução abaixo do saldo devedor atual.
    /// LG-07: appenda histórico somente quando o valor efetivamente muda.
    /// LG-03: revalida coerência das datas após qualquer alteração.
    /// </summary>
    public void Atualizar(
        IClock clock,
        Money? novoLimiteBrl = null,
        LocalDate? novaDataVigenciaInicio = null,
        LocalDate? novaDataVigenciaFim = null,
        string? observacoes = null,
        Money? saldoDevedorAtual = null)
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

            // LG-06: redução abaixo do saldo devedor atual é proibida
            if (saldoDevedorAtual.HasValue && novoLimiteBrl.Value.Valor < saldoDevedorAtual.Value.Valor)
            {
                throw new InvalidOperationException(
                    $"Novo limite global (BRL {novoLimiteBrl.Value.Valor:F2}) é menor que o saldo devedor atual " +
                    $"(BRL {saldoDevedorAtual.Value.Valor:F2}).");
            }

            // LG-07: só appenda histórico quando o valor efetivamente muda
            if (novoLimiteBrl.Value.Valor != ValorLimiteBrlDecimal)
            {
                var valorAnterior = new Money(ValorLimiteBrlDecimal, Moeda.Brl);
                ValorLimiteBrlDecimal = novoLimiteBrl.Value.Valor;
                _historico.Add(LimiteGlobalBancoHistorico.Criar(
                    limiteGlobalBancoId: Id,
                    valorAnteriorBrl: valorAnterior,
                    valorNovoBrl: novoLimiteBrl.Value,
                    registradoEm: clock.GetCurrentInstant(),
                    observacoes: observacoes));
            }
        }

        // LG-03: revalida coerência após possível mudança de datas
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
    /// Encerra a vigência do limite, definindo <see cref="DataVigenciaFim"/>.
    /// LG-08: não permite encerrar uma vigência já encerrada.
    /// LG-08: DataFim não pode ser anterior a DataVigenciaInicio.
    /// </summary>
    public void EncerrarVigencia(LocalDate dataFim, IClock clock)
    {
        // LG-08: vigência já encerrada
        if (DataVigenciaFim.HasValue)
        {
            throw new InvalidOperationException("Vigência já encerrada.");
        }

        // LG-08: dataFim deve ser >= DataVigenciaInicio
        if (dataFim < DataVigenciaInicio)
        {
            throw new ArgumentException(
                "DataFim não pode ser anterior a DataVigenciaInicio.",
                nameof(dataFim));
        }

        DataVigenciaFim = dataFim;
        UpdatedAt = clock.GetCurrentInstant();
    }
}
