using Sgcf.Domain.Common;

namespace Sgcf.Domain.Cotacoes;

/// <summary>
/// Contrato de domain service para consulta de saldos e utilizações de limites bancários.
/// Separa o cálculo de disponibilidade do agregado, evitando que o domínio dependa de repositório.
/// Implementação concreta: <c>Sgcf.Infrastructure.Persistence.Repositories.ConsultaSaldoBancoService</c>.
/// SPEC §3.4 — IConsultaSaldoBanco.
/// </summary>
public interface IConsultaSaldoBanco
{
    /// <summary>
    /// Soma dos saldos devedores em BRL de todos os contratos ativos do banco,
    /// independente de modalidade. Usado quando o banco opera em regime Cenário A
    /// (sem <c>LimiteBanco</c> por modalidade cadastrado).
    /// </summary>
    public Task<Money> CalcularSaldoDevedorBancoAsync(Guid bancoId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Soma de <c>LimiteBanco.ValorUtilizadoBrl</c> para todas as modalidades ativas do banco.
    /// Usado quando o banco opera em regime Cenário B (per-modality).
    /// </summary>
    public Task<Money> CalcularUtilizadoAgregadoModalidadesAsync(Guid bancoId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Soma de <c>LimiteBanco.ValorLimiteBrl</c> dos limites ativos para o banco.
    /// Usado para validar Σ modalidades ≤ global.
    /// <paramref name="excluirLimiteBancoId"/>: exclui um registro específico da soma
    /// (necessário ao atualizar um <c>LimiteBanco</c> para não contar o próprio limite duas vezes).
    /// </summary>
    public Task<Money> CalcularSomaLimitesModalidadesAsync(Guid bancoId, Guid tenantId, Guid? excluirLimiteBancoId = null, CancellationToken ct = default);

    /// <summary>
    /// Indica se o banco está em regime per-modality (Cenário B).
    /// Retorna <c>true</c> quando existe ao menos um <c>LimiteBanco</c> ativo para o banco.
    /// </summary>
    public Task<bool> BancoEmRegimePerModalityAsync(Guid bancoId, Guid tenantId, CancellationToken ct = default);
}
