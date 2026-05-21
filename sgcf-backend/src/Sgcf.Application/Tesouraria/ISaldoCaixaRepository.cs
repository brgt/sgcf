using NodaTime;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Porta de persistência para <see cref="SaldoCaixa"/>.
/// O EF global filter isola automaticamente por tenant_id.
/// </summary>
public interface ISaldoCaixaRepository
{
    /// <summary>
    /// Retorna o saldo da conta na data informada, ou nulo se não registrado.
    /// </summary>
    public Task<SaldoCaixa?> GetAsync(Guid contaId, LocalDate dataReferencia, CancellationToken ct = default);

    /// <summary>
    /// Lista os saldos de uma conta em um intervalo de datas (inclusivo em ambos os extremos).
    /// </summary>
    public Task<IReadOnlyList<SaldoCaixa>> ListByContaAsync(
        Guid contaId,
        LocalDate dataDe,
        LocalDate dataAte,
        CancellationToken ct = default);

    /// <summary>
    /// Lista os saldos de todas as contas do tenant em uma data de referência específica.
    /// Usado para consolidar a posição de caixa diária.
    /// </summary>
    public Task<IReadOnlyList<SaldoCaixa>> ListByDataAsync(LocalDate dataReferencia, CancellationToken ct = default);

    public Task AddAsync(SaldoCaixa saldo, CancellationToken ct = default);
    public Task SaveChangesAsync(CancellationToken ct = default);
}
