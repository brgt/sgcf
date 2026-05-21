namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Item de entrada para o comando de upsert em lote de saldos de caixa.
/// <see cref="DataReferencia"/> deve ser uma data ISO 8601 (yyyy-MM-dd).
/// <see cref="Moeda"/> deve ser um valor do enum <see cref="Domain.Common.Moeda"/> (ex: "Brl", "Usd").
/// </summary>
public sealed record UpsertSaldoCaixaItemDto(
    Guid ContaId,
    string DataReferencia,
    decimal Valor,
    string Moeda,
    string RegistradoPor);
