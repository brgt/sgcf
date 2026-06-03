using Microsoft.EntityFrameworkCore;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="IConsultaSaldoBanco"/> via EF Core + PostgreSQL.
///
/// Todos os métodos recebem <paramref name="tenantId"/> explicitamente e usam
/// <c>IgnoreQueryFilters()</c> + filtro manual, seguindo o padrão dos demais repositórios
/// que precisam de acesso cross-tenant controlado (ex.: <c>AuditLogRepository</c>).
/// </summary>
internal sealed class ConsultaSaldoBancoService(SgcfDbContext context) : IConsultaSaldoBanco
{
    /// <summary>
    /// Soma os saldos devedores (ValorPrincipal) de todos os contratos não encerrados do banco,
    /// independente de modalidade. "Contratos ativos" exclui Liquidado, Cancelado e RefinanciadoTotal —
    /// o mesmo predicado canônico usado em <c>CriarCotacaoCommand</c>.
    /// </summary>
    public async Task<Money> CalcularSaldoDevedorBancoAsync(
        Guid bancoId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        decimal soma = await context.Contratos
            .IgnoreQueryFilters()
            .Where(c =>
                c.TenantId == tenantId
                && c.BancoId == bancoId
                && c.Status != StatusContrato.Liquidado
                && c.Status != StatusContrato.Cancelado
                && c.Status != StatusContrato.RefinanciadoTotal)
            .SumAsync(c => c.ValorPrincipalDecimal, ct);

        return new Money(soma, Moeda.Brl);
    }

    /// <summary>
    /// Soma <c>ValorUtilizadoBrl</c> de todos os limites por modalidade vigentes do banco
    /// (DataVigenciaFim == null). Representa o utilizado agregado quando o banco opera em
    /// regime Cenário B (per-modality).
    /// </summary>
    public async Task<Money> CalcularUtilizadoAgregadoModalidadesAsync(
        Guid bancoId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        decimal? soma = await context.LimitesBanco
            .IgnoreQueryFilters()
            .Where(l =>
                l.TenantId == tenantId
                && l.BancoId == bancoId
                && l.DataVigenciaFim == null)
            .SumAsync(l => (decimal?)l.ValorUtilizadoBrlDecimal, ct);

        return new Money(soma ?? 0m, Moeda.Brl);
    }

    /// <summary>
    /// Soma <c>ValorLimiteBrl</c> dos limites por modalidade vigentes do banco.
    /// Usado para validar que Σ modalidades ≤ limite guarda-chuva (LG-09/LG-13).
    /// <paramref name="excluirLimiteBancoId"/>: exclui o registro em edição para evitar dupla contagem.
    /// </summary>
    public async Task<Money> CalcularSomaLimitesModalidadesAsync(
        Guid bancoId,
        Guid tenantId,
        Guid? excluirLimiteBancoId = null,
        CancellationToken ct = default)
    {
        decimal? soma = await context.LimitesBanco
            .IgnoreQueryFilters()
            .Where(l =>
                l.TenantId == tenantId
                && l.BancoId == bancoId
                && l.DataVigenciaFim == null
                && (excluirLimiteBancoId == null || l.Id != excluirLimiteBancoId.Value))
            .SumAsync(l => (decimal?)l.ValorLimiteBrlDecimal, ct);

        return new Money(soma ?? 0m, Moeda.Brl);
    }

    /// <summary>
    /// Indica se o banco está em regime per-modality (Cenário B).
    /// A partir de SPEC_REGIME_LIMITE_EXPLICITO §4.1 o regime é uma decisão explícita de
    /// cadastro (<see cref="Banco.RegimeLimite"/>), não mais inferida da presença de
    /// <c>LimiteBanco</c>. Retorna <c>true</c> quando o regime é <see cref="RegimeLimiteBanco.PerModalidade"/>.
    /// <c>Banco</c> não é tenant-scoped, portanto <paramref name="tenantId"/> não é usado aqui
    /// (mantido na assinatura por consistência com os demais métodos do contrato).
    /// </summary>
    public async Task<bool> BancoEmRegimePerModalityAsync(
        Guid bancoId,
        Guid tenantId,
        CancellationToken ct = default)
    {
        RegimeLimiteBanco regime = await context.Bancos
            .IgnoreQueryFilters()
            .Where(b => b.Id == bancoId)
            .Select(b => b.RegimeLimite)
            .FirstOrDefaultAsync(ct);

        // Banco inexistente → default PerModalidade (preserva o comportamento histórico).
        return regime == RegimeLimiteBanco.PerModalidade;
    }
}
