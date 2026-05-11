using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Registra o EBITDA mensal da empresa para cálculo do índice Dívida/EBITDA no dashboard executivo.
/// Há no máximo um registro por (ano, mes) — a constraint de unicidade é imposta na camada de banco.
/// </summary>
public sealed class EbitdaMensal : Entity
{
    public int Ano { get; private set; }
    public int Mes { get; private set; }

    /// <summary>Valor armazenado em decimal puro para persistência. Acesse <see cref="ValorBrl"/> para uso tipado.</summary>
    internal decimal ValorBrlDecimal { get; private set; }

    /// <summary>EBITDA do mês em BRL.</summary>
    public Money ValorBrl => new(ValorBrlDecimal, Moeda.Brl);

    public Instant CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;

    private EbitdaMensal() { }

    /// <summary>
    /// Cria um novo registro de EBITDA mensal.
    /// O valor é arredondado a 6 casas decimais (HalfUp) antes de armazenar.
    /// </summary>
    public static EbitdaMensal Criar(int ano, int mes, decimal valorBrl, string createdBy, IClock clock)
    {
        if (valorBrl <= 0)
        {
            throw new ArgumentException("EBITDA deve ser positivo.", nameof(valorBrl));
        }

        if (mes < 1 || mes > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), mes, "Mês deve estar entre 1 e 12.");
        }

        if (ano < 2000 || ano > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(ano), ano, "Ano fora do intervalo esperado.");
        }

        return new EbitdaMensal
        {
            Ano = ano,
            Mes = mes,
            ValorBrlDecimal = Math.Round(valorBrl, 6, MidpointRounding.AwayFromZero),
            CreatedBy = createdBy,
            CreatedAt = clock.GetCurrentInstant()
        };
    }

    /// <summary>Atualiza o valor do EBITDA para este mês (upsert).</summary>
    public void Atualizar(decimal novoValorBrl, string updatedBy, IClock clock)
    {
        if (novoValorBrl <= 0)
        {
            throw new ArgumentException("EBITDA deve ser positivo.", nameof(novoValorBrl));
        }

        ValorBrlDecimal = Math.Round(novoValorBrl, 6, MidpointRounding.AwayFromZero);
        CreatedBy = updatedBy;
        CreatedAt = clock.GetCurrentInstant();
    }
}
