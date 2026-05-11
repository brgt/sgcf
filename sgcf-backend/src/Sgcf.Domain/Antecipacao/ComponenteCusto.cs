using Sgcf.Domain.Common;

namespace Sgcf.Domain.Antecipacao;

/// <summary>
/// Componente de custo individual numa simulação de antecipação.
/// Sinal "+" indica custo adicional; "-" indica abatimento/desconto.
/// </summary>
public sealed record ComponenteCusto(
    string Codigo,
    string Descricao,
    Money Valor,
    string Sinal
);
