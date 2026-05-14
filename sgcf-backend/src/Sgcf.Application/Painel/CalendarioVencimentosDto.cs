namespace Sgcf.Application.Painel;

/// <summary>
/// Uma parcela individual a vencer: data exata, contrato e valores em BRL.
/// Principal e Juros do mesmo vencimento/contrato são combinados em um único item.
/// <c>JurosBrlProjetado</c> é preenchido quando <c>cdiAnualPct</c> é informado na query
/// e o contrato tem juros indexados ao CDI (valor original zero).
/// </summary>
public sealed record VencimentoItemDto(
    string Data,
    Guid ContratoId,
    string NumeroContrato,
    decimal PrincipalBrl,
    decimal JurosBrl,
    decimal TotalBrl,
    decimal? JurosBrlProjetado = null);

/// <summary>Totais de principal e juros a vencer em um mês específico, com o detalhe por dia.</summary>
public sealed record MesVencimentoDto(
    int Ano,
    int Mes,
    decimal TotalPrincipalBrl,
    decimal TotalJurosBrl,
    decimal TotalBrl,
    int QuantidadeParcelas,
    IReadOnlyList<VencimentoItemDto> Parcelas,
    decimal? TotalJurosBrlProjetado = null);

/// <summary>
/// Calendário de vencimentos de parcelas abertas para um ano civil,
/// agrupadas por mês, com totais em BRL (convertidos via spot ou PTAX).
/// <c>TaxaCdiUsadaPct</c> e os campos <c>*Projetado</c> são preenchidos quando
/// <c>cdiAnualPct</c> é informado na query.
/// </summary>
public sealed record CalendarioVencimentosDto(
    int Ano,
    IReadOnlyList<MesVencimentoDto> Meses,
    decimal TotalAnoBrl,
    decimal? TaxaCdiUsadaPct = null);
