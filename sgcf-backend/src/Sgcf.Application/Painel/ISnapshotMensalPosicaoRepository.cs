using Sgcf.Domain.Painel;

namespace Sgcf.Application.Painel;

public interface ISnapshotMensalPosicaoRepository
{
    /// <summary>
    /// Verifica se já existe um snapshot para o (ano, mes) indicado.
    /// Garante idempotência do job mensal.
    /// </summary>
    public Task<bool> ExisteParaMesAsync(int ano, int mes, CancellationToken ct = default);

    public void Add(SnapshotMensalPosicao snap);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
