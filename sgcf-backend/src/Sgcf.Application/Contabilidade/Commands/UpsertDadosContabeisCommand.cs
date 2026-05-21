using MediatR;

namespace Sgcf.Application.Contabilidade.Commands;

/// <summary>
/// Cadastra ou atualiza os dados contábeis de um mês de competência específico.
/// </summary>
/// <param name="Ano">Ano da competência (2000–2100).</param>
/// <param name="Mes">Mês da competência (1–12).</param>
/// <param name="PatrimonioLiquidoBrl">Patrimônio Líquido em BRL. Pode ser negativo.</param>
/// <param name="DespesaFinanceiraBrl">Despesa Financeira em BRL. Deve ser não-negativa.</param>
public sealed record UpsertDadosContabeisCommand(
    int Ano,
    int Mes,
    decimal PatrimonioLiquidoBrl,
    decimal DespesaFinanceiraBrl) : IRequest<Unit>;
