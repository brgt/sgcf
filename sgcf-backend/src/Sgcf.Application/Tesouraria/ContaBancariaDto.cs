using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Representação de leitura de uma <see cref="ContaBancaria"/>.
/// Expõe todos os campos públicos da entidade; timestamps convertidos para
/// <see cref="DateTimeOffset"/> para serialização JSON uniforme.
/// </summary>
public sealed record ContaBancariaDto(
    Guid Id,
    Guid TenantId,
    Guid BancoId,
    string Nome,
    string Agencia,
    string NumeroConta,
    string Moeda,
    bool Ativa,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm)
{
    public static ContaBancariaDto From(ContaBancaria conta) =>
        new(
            conta.Id,
            conta.TenantId,
            conta.BancoId,
            conta.Nome,
            conta.Agencia,
            conta.NumeroConta,
            conta.Moeda.ToString(),
            conta.Ativa,
            conta.CriadoEm.ToDateTimeOffset(),
            conta.AtualizadoEm.ToDateTimeOffset());
}
