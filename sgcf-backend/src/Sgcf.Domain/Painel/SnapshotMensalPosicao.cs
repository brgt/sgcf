using NodaTime;
using Sgcf.Domain.Common;

namespace Sgcf.Domain.Painel;

/// <summary>
/// Snapshot mensal da posição consolidada da carteira de contratos.
/// Gerado no último dia do mês pelo job diário.
/// Idempotência garantida por UNIQUE constraint em (ano, mes).
/// </summary>
public sealed class SnapshotMensalPosicao : Entity
{
    public int Ano { get; private set; }
    public int Mes { get; private set; }
    public int TotalContratosAtivos { get; private set; }

    internal decimal SaldoPrincipalBrlDecimal { get; private set; }
    internal decimal TotalParcelasAbertasBrlDecimal { get; private set; }

    /// <summary>Soma dos saldos principais de contratos ativos convertidos para BRL via PTAX D-1.</summary>
    public Money SaldoPrincipalBrl => new(SaldoPrincipalBrlDecimal, Moeda.Brl);

    /// <summary>Soma dos valores das parcelas abertas (Status=Previsto) convertidas para BRL.</summary>
    public Money TotalParcelasAbertasBrl => new(TotalParcelasAbertasBrlDecimal, Moeda.Brl);

    public Instant CriadoEm { get; private set; }

    private SnapshotMensalPosicao() { }

    /// <summary>
    /// Cria um snapshot mensal de posição.
    /// </summary>
    public static SnapshotMensalPosicao Criar(
        int ano,
        int mes,
        int totalContratosAtivos,
        decimal saldoPrincipalBrl,
        decimal totalParcelasAbertasBrl,
        IClock clock)
    {
        if (mes < 1 || mes > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), mes, "Mês deve estar entre 1 e 12.");
        }

        if (ano < 2000 || ano > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(ano), ano, "Ano fora do intervalo esperado.");
        }

        if (totalContratosAtivos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalContratosAtivos), "Não pode ser negativo.");
        }

        return new SnapshotMensalPosicao
        {
            Ano = ano,
            Mes = mes,
            TotalContratosAtivos = totalContratosAtivos,
            SaldoPrincipalBrlDecimal = Math.Round(saldoPrincipalBrl, 6, MidpointRounding.AwayFromZero),
            TotalParcelasAbertasBrlDecimal = Math.Round(totalParcelasAbertasBrl, 6, MidpointRounding.AwayFromZero),
            CriadoEm = clock.GetCurrentInstant()
        };
    }
}
