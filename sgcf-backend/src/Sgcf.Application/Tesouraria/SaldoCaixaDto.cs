using NodaTime;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Projeção de leitura de <see cref="SaldoCaixa"/> para a camada de API.
/// Datas expressas como string ISO 8601 para serialização consistente.
/// </summary>
public sealed record SaldoCaixaDto(
    Guid Id,
    Guid ContaId,
    string DataReferencia,
    decimal Valor,
    string Moeda,
    string RegistradoPor,
    DateTimeOffset RegistradoEm)
{
    /// <summary>Projeta um <see cref="SaldoCaixa"/> para DTO de leitura.</summary>
    public static SaldoCaixaDto From(SaldoCaixa saldo) =>
        new(
            Id: saldo.Id,
            ContaId: saldo.ContaId,
            DataReferencia: saldo.DataReferencia.ToString("yyyy-MM-dd", null),
            Valor: saldo.Valor.Valor,
            Moeda: saldo.Valor.Moeda.ToString().ToUpperInvariant(),
            RegistradoPor: saldo.RegistradoPor,
            RegistradoEm: saldo.RegistradoEm.ToDateTimeOffset());
}
