using NodaTime;

using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cronograma;

/// <summary>
/// Parcela de um cronograma customizado com datas e valores definidos externamente.
/// </summary>
public sealed record ParcelaCustomizada(
    int Numero,
    LocalDate DataPrevista,
    Money ValorPrincipal,
    Money ValorJuros
);

/// <summary>
/// Dados de entrada para cronograma customizado (datas livres, importadas externamente).
/// Utilizado em contratos Balcão Caixa com datas de pagamento definidas por duplicatas.
/// </summary>
public sealed record EntradaCustomizada(
    Money ValorPrincipal,
    IReadOnlyList<ParcelaCustomizada> Parcelas
);
