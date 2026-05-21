using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

/// <summary>
/// Repositório para o modelo global de plano de contas.
///
/// O modelo é global (não tenant-scoped): queries não aplicam filtro de tenant.
/// Operações de escrita são restritas a super-admin.
/// </summary>
public interface IPlanoContasModeloRepository
{
    public Task<PlanoContasModelo?> GetByCodigoAsync(string codigoGerencial, CancellationToken ct = default);
    public Task<IReadOnlyList<PlanoContasModelo>> ListAllAsync(CancellationToken ct = default);
    public void Add(PlanoContasModelo modelo);
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
