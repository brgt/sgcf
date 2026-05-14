using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Dados de entrada para geração do cronograma Price (parcelas totais iguais).
/// </summary>
/// <param name="ValorPrincipal">Valor financiado.</param>
/// <param name="TaxaMensal">Taxa mensal efetiva (a.m.). O chamador é responsável por converter a.a. → a.m. se necessário.</param>
/// <param name="DataDesembolso">Data de liberação dos recursos.</param>
/// <param name="DataPrimeiroVencimento">Data de vencimento da primeira parcela.</param>
/// <param name="NumeroParcelas">Quantidade de parcelas mensais.</param>
/// <param name="AliqIrrf">Alíquota de IRRF, se aplicável. Quando informada, gera evento IrrfRetido por período (gross-up).</param>
public sealed record EntradaPrice(
    Money ValorPrincipal,
    Percentual TaxaMensal,
    LocalDate DataDesembolso,
    LocalDate DataPrimeiroVencimento,
    int NumeroParcelas,
    Percentual? AliqIrrf = null
);
