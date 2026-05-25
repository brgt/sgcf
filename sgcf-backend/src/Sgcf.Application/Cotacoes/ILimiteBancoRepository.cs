using NodaTime;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Porta de persistência para o agregado <see cref="LimiteBanco"/>.
/// Implementação pertence a Sgcf.Infrastructure.
/// </summary>
public interface ILimiteBancoRepository
{
    public void Add(LimiteBanco limite);
    public void Update(LimiteBanco limite);

    /// <summary>Retorna o limite vigente para banco+modalidade, ou null se não cadastrado.</summary>
    public Task<LimiteBanco?> GetByBancoModalidadeAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o primeiro limite do par bancoId+modalidade cujo período [DataVigenciaInicio, DataVigenciaFim]
    /// se sobrepõe ao período [inicio, fim], ou null se não houver sobreposição.
    /// Sobreposição: <c>inicio &lt;= existente.Fim AND existente.Inicio &lt;= fim</c>.
    /// Trata DataVigenciaFim == null como "infinito" (aberto).
    /// Exclui opcionalmente o próprio limite (para uso em updates).
    /// </summary>
    public Task<LimiteBanco?> FindOverlappingAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        LocalDate inicio,
        LocalDate? fim,
        Guid? excluirId = null,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<LimiteBanco>> ListAsync(
        Guid? bancoId,
        ModalidadeContrato? modalidade,
        CancellationToken cancellationToken = default);

    public Task<LimiteBanco?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o limite por id com tracking habilitado (para uso em comandos de atualização).
    /// Faz eager-load de GarantiasExigidas e Historico.
    /// </summary>
    public Task<LimiteBanco?> GetByIdTrackingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todas as revisões de garantias exigidas do limite especificado,
    /// ordenadas por VigenciaInicio ascendente (SLB-05).
    /// Inclui eager-load dos Itens de cada revisão.
    /// </summary>
    public Task<IReadOnlyList<GarantiaExigidaRevisao>> GetRevisoesGarantiasAsync(
        Guid limiteBancoId,
        CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
