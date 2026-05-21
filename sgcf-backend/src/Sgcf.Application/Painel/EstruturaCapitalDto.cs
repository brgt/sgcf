using Sgcf.Application.Common;

namespace Sgcf.Application.Painel;

/// <summary>
/// Estrutura de capital consolidada da empresa.
/// Inclui indicadores de alavancagem e cobertura de despesa financeira (ICR).
/// </summary>
/// <param name="DividaTotalBrl">Dívida bruta total convertida para BRL.</param>
/// <param name="PatrimonioLiquidoBrl">Soma do Patrimônio Líquido dos últimos 12 meses / mês atual.</param>
/// <param name="DividaSobrePatrimonio">
/// Índice Dívida / PL. Retorna 0 quando PL é zero para evitar divisão por zero.
/// </param>
/// <param name="EbitdaUltimos12mBrl">Soma do EBITDA dos últimos 12 meses em BRL.</param>
/// <param name="DespesaFinanceira12mBrl">Soma da Despesa Financeira dos últimos 12 meses em BRL.</param>
/// <param name="Icr">
/// Índice de Cobertura de Receitas = EBITDA12m / DespesaFinanceira12m.
/// Retorna 0 quando DespesaFinanceira12m é zero.
/// </param>
/// <param name="Alertas">Alertas de qualidade de dados (ex: "DADOS_CONTABEIS_AUSENTES").</param>
/// <param name="Completude">Grau de completude dos dados retornados.</param>
public sealed record EstruturaCapitalDto(
    decimal DividaTotalBrl,
    decimal PatrimonioLiquidoBrl,
    decimal DividaSobrePatrimonio,
    decimal EbitdaUltimos12mBrl,
    decimal DespesaFinanceira12mBrl,
    decimal Icr,
    IReadOnlyList<string> Alertas,
    Completude Completude);
