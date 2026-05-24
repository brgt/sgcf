using NodaTime;
using Sgcf.Domain.Auditoria;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Entrada de histórico do valor do limite guarda-chuva concedido pelo banco.
/// Cada alteração de <see cref="LimiteGlobalBanco.ValorLimiteBrl"/> gera uma entrada,
/// permitindo análise de tendência (bancos que aumentam ou reduzem o teto global).
/// Entidade append-only — nunca mutada após a criação.
/// SPEC §3.3 — LimiteGlobalBancoHistorico.
/// </summary>
public sealed class LimiteGlobalBancoHistorico : Entity, IAuditable
{
    public Guid LimiteGlobalBancoId { get; private set; }

    internal decimal? ValorAnteriorBrlDecimal { get; private set; }

    /// <summary>Valor do limite antes da alteração. Null quando é a entrada inicial (criação do limite global).</summary>
    public Money? ValorAnteriorBrl =>
        ValorAnteriorBrlDecimal.HasValue ? new Money(ValorAnteriorBrlDecimal.Value, Moeda.Brl) : null;

    internal decimal ValorNovoBrlDecimal { get; private set; }
    public Money ValorNovoBrl => new(ValorNovoBrlDecimal, Moeda.Brl);

    public Instant RegistradoEm { get; private set; }
    public string? Observacoes { get; private set; }

    /// <summary>Construtor privado para EF Core.</summary>
    private LimiteGlobalBancoHistorico() { }

    internal static LimiteGlobalBancoHistorico Criar(
        Guid limiteGlobalBancoId,
        Money? valorAnteriorBrl,
        Money valorNovoBrl,
        Instant registradoEm,
        string? observacoes = null)
    {
        if (limiteGlobalBancoId == Guid.Empty)
        {
            throw new ArgumentException("LimiteGlobalBancoId não pode ser vazio.", nameof(limiteGlobalBancoId));
        }

        if (valorNovoBrl.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorNovoBrl deve ser em BRL.", nameof(valorNovoBrl));
        }

        if (valorAnteriorBrl.HasValue && valorAnteriorBrl.Value.Moeda != Moeda.Brl)
        {
            throw new ArgumentException("ValorAnteriorBrl deve ser em BRL.", nameof(valorAnteriorBrl));
        }

        return new LimiteGlobalBancoHistorico
        {
            LimiteGlobalBancoId = limiteGlobalBancoId,
            ValorAnteriorBrlDecimal = valorAnteriorBrl?.Valor,
            ValorNovoBrlDecimal = valorNovoBrl.Valor,
            RegistradoEm = registradoEm,
            Observacoes = observacoes,
        };
    }
}
