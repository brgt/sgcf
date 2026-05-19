using NodaTime;
using Sgcf.Domain.Sistema;

namespace Sgcf.Application.Sistema;

/// <summary>
/// Repositório para a entidade singleton <see cref="ParametroSistema"/>.
/// Garante que sempre existe exatamente uma linha na tabela (padrão get-or-create).
/// </summary>
public interface IParametroSistemaRepository
{
    /// <summary>
    /// Retorna o singleton de parâmetros do sistema.
    /// Se ainda não existir, cria e persiste o registro padrão (sem tetão).
    /// </summary>
    public Task<ParametroSistema> GetOrCreateGlobalAsync(IClock clock, CancellationToken ct = default);

    /// <summary>Persiste alterações pendentes no contexto.</summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
