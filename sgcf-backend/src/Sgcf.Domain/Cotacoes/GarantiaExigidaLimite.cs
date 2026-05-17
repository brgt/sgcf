using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Requisito de garantia exigido pelo banco para liberar uma linha de crédito (LimiteBanco).
/// Child entity owned by LimiteBanco — uma linha pode ter zero, uma ou várias garantias exigidas.
///
/// AD-4 (relaxada): para tipos diferentes de Aval, exatamente um entre
/// <see cref="PercentualSobreLimite"/> ou <see cref="ValorFixoBrl"/> deve ser informado.
/// Para Aval, ambos podem ser nulos (representa exigência implícita de aval pelos sócios
/// cobrindo 100% da exposição da linha).
/// </summary>
public sealed class GarantiaExigidaLimite : Entity, IAuditable
{
    public Guid LimiteBancoId { get; private set; }
    public TipoGarantia Tipo { get; private set; }

    /// <summary>Percentual sobre o limite, em valor humano (ex: 20 = 20%). Exclusivo com ValorFixoBrl.</summary>
    public decimal? PercentualSobreLimite { get; private set; }

    internal decimal? ValorFixoBrlDecimal { get; private set; }

    /// <summary>Valor fixo em BRL exigido como garantia. Exclusivo com PercentualSobreLimite.</summary>
    public Money? ValorFixoBrl =>
        ValorFixoBrlDecimal.HasValue ? new Money(ValorFixoBrlDecimal.Value, Moeda.Brl) : null;

    public bool Obrigatoria { get; private set; }
    public string? Observacoes { get; private set; }

    public Instant CreatedAt { get; private set; }
    public Instant UpdatedAt { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private GarantiaExigidaLimite() { }

    public static GarantiaExigidaLimite Criar(
        Guid limiteBancoId,
        TipoGarantia tipo,
        decimal? percentualSobreLimite,
        Money? valorFixoBrl,
        bool obrigatoria,
        string? observacoes,
        IClock clock)
    {
        if (limiteBancoId == Guid.Empty)
        {
            throw new ArgumentException("LimiteBancoId não pode ser vazio.", nameof(limiteBancoId));
        }

        ValidarCamposExclusivos(tipo, percentualSobreLimite, valorFixoBrl);

        var now = clock.GetCurrentInstant();
        return new GarantiaExigidaLimite
        {
            LimiteBancoId = limiteBancoId,
            Tipo = tipo,
            PercentualSobreLimite = percentualSobreLimite,
            ValorFixoBrlDecimal = valorFixoBrl?.Valor,
            Obrigatoria = obrigatoria,
            Observacoes = observacoes,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Atualizar(
        decimal? percentualSobreLimite,
        Money? valorFixoBrl,
        bool obrigatoria,
        string? observacoes,
        IClock clock)
    {
        ValidarCamposExclusivos(Tipo, percentualSobreLimite, valorFixoBrl);

        PercentualSobreLimite = percentualSobreLimite;
        ValorFixoBrlDecimal = valorFixoBrl?.Valor;
        Obrigatoria = obrigatoria;
        Observacoes = observacoes;
        UpdatedAt = clock.GetCurrentInstant();
    }

    private static void ValidarCamposExclusivos(
        TipoGarantia tipo,
        decimal? percentualSobreLimite,
        Money? valorFixoBrl)
    {
        bool temPercentual = percentualSobreLimite.HasValue;
        bool temValorFixo = valorFixoBrl.HasValue;

        if (temPercentual && temValorFixo)
        {
            throw new ArgumentException(
                "Campos percentual e valor fixo são mutuamente exclusivos — informe apenas um.",
                nameof(percentualSobreLimite));
        }

        if (!temPercentual && !temValorFixo && tipo != TipoGarantia.Aval)
        {
            throw new ArgumentException(
                "Informe percentual ou valor fixo (exceto para garantias do tipo Aval).",
                nameof(percentualSobreLimite));
        }

        if (temPercentual && (percentualSobreLimite!.Value <= 0m || percentualSobreLimite.Value > 100m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(percentualSobreLimite),
                "Percentual deve estar no intervalo (0, 100].");
        }

        if (temValorFixo)
        {
            if (valorFixoBrl!.Value.Moeda != Moeda.Brl)
            {
                throw new ArgumentException("ValorFixoBrl deve ser em BRL.", nameof(valorFixoBrl));
            }

            if (valorFixoBrl.Value.Valor <= 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(valorFixoBrl),
                    "ValorFixoBrl deve ser positivo.");
            }
        }
    }
}
