using Sgcf.Domain.Alertas;

namespace Sgcf.Application.Alertas;

/// <summary>
/// Contrato de persistência para o agregado <see cref="Alerta"/>.
/// </summary>
public interface IAlertaRepository
{
    /// <summary>Busca um alerta pelo seu Id dentro do tenant corrente.</summary>
    public Task<Alerta?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Lista alertas com filtros opcionais e paginação.</summary>
    public Task<IReadOnlyList<Alerta>> ListAsync(AlertaFilter filter, CancellationToken ct);

    /// <summary>
    /// Retorna os contadores de alertas abertos por severidade para um perfil específico.
    /// Usado pelo cockpit para exibir badges de notificação.
    /// </summary>
    public Task<ContadoresAlerta> GetContadoresAsync(PerfilCockpit perfil, CancellationToken ct);

    /// <summary>Adiciona o alerta ao contexto para persistência no próximo SaveChanges.</summary>
    public Task AddAsync(Alerta alerta, CancellationToken ct);

    /// <summary>Persiste as alterações pendentes no banco de dados.</summary>
    public Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Tenta adicionar o alerta de forma idempotente.
    /// Retorna <c>false</c> sem lançar exceção se a <see cref="Alerta.ChaveIdempotencia"/> já existir.
    /// Retorna <c>true</c> quando o alerta for inserido com sucesso.
    /// </summary>
    public Task<bool> TryAddIdempotentAsync(Alerta alerta, CancellationToken ct);
}

/// <summary>
/// Parâmetros de filtragem e paginação para listagem de alertas.
/// </summary>
public sealed record AlertaFilter(
    PerfilCockpit? Perfil = null,
    SeveridadeAlerta? Severidade = null,
    CategoriaAlerta? Categoria = null,
    StatusAlerta? Status = null,
    int PageNumber = 1,
    int PageSize = 20);

/// <summary>
/// Contagem de alertas abertos agrupados por severidade,
/// retornada pelo endpoint de badges do cockpit.
/// </summary>
public sealed record ContadoresAlerta(int Critico, int Atencao, int Informativo);
