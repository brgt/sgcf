using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Porta de persistência para o agregado <see cref="LimiteGlobalBanco"/>.
/// Implementação pertence a Sgcf.Infrastructure.
/// SPEC §3.2 — AD-03.
/// </summary>
public interface ILimiteGlobalBancoRepository
{
    public void Add(LimiteGlobalBanco limite);

    /// <summary>Retorna o limite global por id, sem tracking, com Historico carregado.</summary>
    public Task<LimiteGlobalBanco?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Retorna o limite global por id, com tracking habilitado e Historico carregado. Usar em comandos de atualização.</summary>
    public Task<LimiteGlobalBanco?> GetByIdTrackingAsync(Guid id, CancellationToken ct = default);

    /// <summary>Retorna o limite vigente (DataVigenciaFim == null) para o banco, sem tracking.</summary>
    public Task<LimiteGlobalBanco?> GetVigenteByBancoAsync(Guid bancoId, CancellationToken ct = default);

    /// <summary>
    /// Retorna o primeiro limite que se sobrepõe ao período [inicio, fim] para o banco.
    /// Sobreposição: (fim == null || fim >= l.DataVigenciaInicio) &amp;&amp; (l.DataVigenciaFim == null || l.DataVigenciaFim >= inicio).
    /// Exclui opcionalmente o próprio id (para uso em updates).
    /// </summary>
    public Task<LimiteGlobalBanco?> FindOverlappingAsync(
        Guid bancoId,
        LocalDate inicio,
        LocalDate? fim,
        Guid? excluirId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lista limites globais. Filtra opcionalmente por banco e/ou data de vigência.
    /// Quando <paramref name="vigentesEm"/> é fornecido, retorna apenas registros cujo
    /// período [DataVigenciaInicio, DataVigenciaFim] contém a data informada.
    /// </summary>
    public Task<IReadOnlyList<LimiteGlobalBanco>> ListAsync(
        Guid? bancoId,
        LocalDate? vigentesEm,
        CancellationToken ct = default);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
