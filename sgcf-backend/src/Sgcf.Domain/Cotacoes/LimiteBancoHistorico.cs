using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Entrada de histórico do valor do limite concedido pelo banco.
/// Cada alteração de <see cref="LimiteBanco.ValorLimiteBrl"/> gera uma entrada,
/// permitindo análise de tendência (bancos que aumentam ou reduzem nosso limite).
/// </summary>
public sealed class LimiteBancoHistorico : Entity, IAuditable
{
    public Guid LimiteBancoId { get; private set; }

    internal decimal? ValorAnteriorBrlDecimal { get; private set; }

    /// <summary>Valor do limite antes da alteração. Null quando é a entrada inicial (criação do limite).</summary>
    public Money? ValorAnteriorBrl =>
        ValorAnteriorBrlDecimal.HasValue ? new Money(ValorAnteriorBrlDecimal.Value, Moeda.Brl) : null;

    internal decimal ValorNovoBrlDecimal { get; private set; }
    public Money ValorNovoBrl => new(ValorNovoBrlDecimal, Moeda.Brl);

    public Instant RegistradoEm { get; private set; }
    public string? Observacoes { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private LimiteBancoHistorico() { }

    internal static LimiteBancoHistorico Criar(
        Guid limiteBancoId,
        Money? valorAnteriorBrl,
        Money valorNovoBrl,
        Instant registradoEm,
        string? observacoes = null)
    {
        if (limiteBancoId == Guid.Empty)
        {
            throw new ArgumentException("LimiteBancoId não pode ser vazio.", nameof(limiteBancoId));
        }

        if (valorNovoBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorNovoBrl deve ser em BRL.", nameof(valorNovoBrl));
        }

        if (valorAnteriorBrl.HasValue && valorAnteriorBrl.Value.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorAnteriorBrl deve ser em BRL.", nameof(valorAnteriorBrl));
        }

        return new LimiteBancoHistorico
        {
            LimiteBancoId = limiteBancoId,
            ValorAnteriorBrlDecimal = valorAnteriorBrl?.Valor,
            ValorNovoBrlDecimal = valorNovoBrl.Valor,
            RegistradoEm = registradoEm,
            Observacoes = observacoes,
        };
    }
}
