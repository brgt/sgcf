using NodaTime;

namespace Sgcf.Application.Painel.Queries;

/// <summary>
/// Saldo atual da carteira de contratos agrupado por banco, convertido para BRL.
/// Usado como entrada do ProjetorSaldoMensal (Task 1.1) e como resposta pública quando necessário.
/// </summary>
public sealed record SaldoPorBancoAtualDto(
    IReadOnlyList<SaldoBancoAtualDto> Bancos,
    decimal SaldoTotalBrl,
    LocalDate DataReferencia);

/// <summary>
/// Saldo de um banco específico na data de referência, em BRL.
/// </summary>
public sealed record SaldoBancoAtualDto(
    Guid BancoId,
    string BancoApelido,
    string BancoCodigoCompe,
    decimal SaldoBrl,
    int QuantidadeContratosAtivos);
