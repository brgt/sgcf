using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Domain.Contabilidade;

/// <summary>
/// Registra os dados contábeis mensais da empresa: Patrimônio Líquido e Despesa Financeira.
/// Usados para cálculo do Índice de Cobertura de Receitas (ICR) = EBITDA / DespesaFinanceira.
/// Há no máximo um registro por (tenant_id, ano, mês) — constraint de unicidade imposta no banco.
/// </summary>
public sealed class DadosContabeisMensal : Entity, ITenantScoped
{
    public Guid TenantId { get; private set; }

    /// <summary>Ano da competência.</summary>
    public int Ano { get; private set; }

    /// <summary>Mês da competência (1–12).</summary>
    public int Mes { get; private set; }

    /// <summary>
    /// Valor decimal armazenado para persistência — não expor diretamente.
    /// Use <see cref="PatrimonioLiquido"/> para acesso tipado.
    /// </summary>
    internal decimal PatrimonioLiquidoDecimal { get; private set; }

    /// <summary>
    /// Valor decimal armazenado para persistência — não expor diretamente.
    /// Use <see cref="DespesaFinanceira"/> para acesso tipado.
    /// </summary>
    internal decimal DespesaFinanceiraDecimal { get; private set; }

    /// <summary>Patrimônio Líquido do mês em BRL.</summary>
    public Money PatrimonioLiquido => new(PatrimonioLiquidoDecimal, Moeda.Brl);

    /// <summary>
    /// Despesa Financeira do mês em BRL.
    /// Pode ser zero — o handler deve tratar divisão por zero ao calcular ICR.
    /// </summary>
    public Money DespesaFinanceira => new(DespesaFinanceiraDecimal, Moeda.Brl);

    public Instant CriadoEm { get; private set; }
    public Instant AtualizadoEm { get; private set; }

    private DadosContabeisMensal() { }

    /// <summary>
    /// Cria um novo registro de dados contábeis mensais.
    /// Aceita Patrimônio Líquido negativo (empresa alavancada) e Despesa Financeira zero.
    /// </summary>
    public static DadosContabeisMensal Criar(
        int ano,
        int mes,
        Money patrimonioLiquido,
        Money despesaFinanceira,
        IClock clock)
    {
        ValidarCompetencia(ano, mes);
        ValidarMoeda(patrimonioLiquido, nameof(patrimonioLiquido));
        ValidarMoeda(despesaFinanceira, nameof(despesaFinanceira));

        if (despesaFinanceira.Valor < 0m)
        {
            throw new ArgumentException("Despesa Financeira não pode ser negativa.", nameof(despesaFinanceira));
        }

        Instant agora = clock.GetCurrentInstant();

        return new DadosContabeisMensal
        {
            Ano = ano,
            Mes = mes,
            PatrimonioLiquidoDecimal = Math.Round(patrimonioLiquido.Valor, 6, MidpointRounding.AwayFromZero),
            DespesaFinanceiraDecimal = Math.Round(despesaFinanceira.Valor, 6, MidpointRounding.AwayFromZero),
            CriadoEm = agora,
            AtualizadoEm = agora
        };
    }

    /// <summary>
    /// Atualiza os valores de Patrimônio Líquido e Despesa Financeira (operação de upsert).
    /// </summary>
    public void Atualizar(Money patrimonioLiquido, Money despesaFinanceira, IClock clock)
    {
        ValidarMoeda(patrimonioLiquido, nameof(patrimonioLiquido));
        ValidarMoeda(despesaFinanceira, nameof(despesaFinanceira));

        if (despesaFinanceira.Valor < 0m)
        {
            throw new ArgumentException("Despesa Financeira não pode ser negativa.", nameof(despesaFinanceira));
        }

        PatrimonioLiquidoDecimal = Math.Round(patrimonioLiquido.Valor, 6, MidpointRounding.AwayFromZero);
        DespesaFinanceiraDecimal = Math.Round(despesaFinanceira.Valor, 6, MidpointRounding.AwayFromZero);
        AtualizadoEm = clock.GetCurrentInstant();
    }

    private static void ValidarCompetencia(int ano, int mes)
    {
        if (mes < 1 || mes > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), mes, "Mês deve estar entre 1 e 12.");
        }

        if (ano < 2000 || ano > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(ano), ano, "Ano fora do intervalo esperado.");
        }
    }

    private static void ValidarMoeda(Money valor, string paramName)
    {
        if (valor.Moeda != Moeda.Brl)
        {
            throw new ArgumentException(
                $"Apenas BRL é aceito para dados contábeis. Recebido: {valor.Moeda}.",
                paramName);
        }
    }
}
