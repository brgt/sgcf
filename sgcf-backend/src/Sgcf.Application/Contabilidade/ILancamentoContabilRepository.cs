using NodaTime;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

public interface ILancamentoContabilRepository
{
    /// <summary>
    /// Verifica se já existe um lançamento com a mesma chave (contrato_id, data, origem).
    /// Garante idempotência do job diário de provisão.
    /// </summary>
    public Task<bool> ExisteAsync(Guid contratoId, LocalDate data, string origem, CancellationToken ct = default);

    public Task<IReadOnlyList<LancamentoContabil>> ListByContratoAsync(Guid contratoId, CancellationToken ct = default);

    public void Add(LancamentoContabil lancamento);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
