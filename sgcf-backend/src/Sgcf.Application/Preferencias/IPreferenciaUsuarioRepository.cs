using Sgcf.Domain.Preferencias;

namespace Sgcf.Application.Preferencias;

/// <summary>
/// Repositório para <see cref="PreferenciaUsuario"/>.
///
/// O EF Core global query filter garante que todos os métodos de leitura
/// retornam apenas dados do tenant ativo no contexto atual — nenhum parâmetro
/// de tenant é passado explicitamente.
/// </summary>
public interface IPreferenciaUsuarioRepository
{
    /// <summary>
    /// Retorna todas as preferências do usuário informado no tenant atual.
    /// </summary>
    public Task<IReadOnlyList<PreferenciaUsuario>> ListByUserIdAsync(string userId, CancellationToken ct);

    /// <summary>
    /// Retorna a preferência identificada por (userId, chave) no tenant atual.
    /// Retorna <c>null</c> quando não existe.
    /// </summary>
    public Task<PreferenciaUsuario?> GetAsync(string userId, string chave, CancellationToken ct);

    /// <summary>Adiciona uma nova preferência ao contexto (sem salvar).</summary>
    public void Add(PreferenciaUsuario p);

    /// <summary>Remove uma preferência do contexto (sem salvar).</summary>
    public void Remove(PreferenciaUsuario p);

    /// <summary>Persiste alterações pendentes no contexto.</summary>
    public Task<int> SaveChangesAsync(CancellationToken ct);
}
