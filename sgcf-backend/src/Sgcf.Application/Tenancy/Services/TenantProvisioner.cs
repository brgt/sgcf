using Microsoft.Extensions.Logging;
using NodaTime;
using Sgcf.Application.Cambio;
using Sgcf.Application.Sistema;
using Sgcf.Domain.Cambio;
using Sgcf.Domain.Sistema;
using Sgcf.Domain.Tenancy;

namespace Sgcf.Application.Tenancy.Services;

/// <summary>
/// Implementação de <see cref="ITenantProvisioner"/> que semeia os dados mestres
/// mínimos necessários para que um tenant possa operar no sistema.
///
/// Categorias provisionadas (chaves camelCase no ResultadoProvisionamento):
/// - <c>parametrosSistema</c>: singleton de parâmetros operacionais (ParametroSistema).
/// - <c>parametrosCotacao</c>: regra padrão de cotação PTAX D-1 (ParametroCotacao).
/// - <c>planoContas</c>: postergado para Task -1.10 (PlanoContasModelo não existe ainda).
///
/// Idempotência: cada categoria verifica se já existe antes de inserir.
/// O TenantId é definido explicitamente nas entidades porque o provisionador opera
/// fora do contexto de request do tenant alvo — o TenantSaveInterceptor não está ativo.
/// </summary>
public sealed partial class TenantProvisioner(
    ITenantRepository tenantRepo,
    IParametroSistemaRepository parametroSistemaRepo,
    IParametroCotacaoRepository parametroCotacaoRepo,
    IClock clock,
    ILogger<TenantProvisioner> logger) : ITenantProvisioner
{
    public async Task<ResultadoProvisionamento> ProvisionarAsync(Guid tenantId, CancellationToken ct)
    {
        Tenant tenant = await tenantRepo.GetAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} não encontrado.");

        if (tenant.Status == StatusTenant.Arquivado)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenant.Slug}' está arquivado e não pode ser provisionado.");
        }

        if (tenant.Status == StatusTenant.Suspenso)
        {
            throw new InvalidOperationException(
                $"Tenant '{tenant.Slug}' está suspenso. Reative o tenant antes de provisionar.");
        }

        Dictionary<string, int> criados = [];
        Dictionary<string, int> ignorados = [];

        await SeedParametroSistemaAsync(tenant, criados, ignorados, ct);
        await SeedParametroCotacaoAsync(tenant, criados, ignorados, ct);
        SeedPlanoContas(criados, ignorados);

        await parametroSistemaRepo.SaveChangesAsync(ct);

        LogProvisionamentoConcluido(logger, tenant.Slug, criados["parametrosSistema"], criados["parametrosCotacao"]);

        return new ResultadoProvisionamento(
            tenant.Id,
            tenant.Slug,
            criados,
            ignorados,
            clock.GetCurrentInstant());
    }

    private async Task SeedParametroSistemaAsync(
        Tenant tenant,
        Dictionary<string, int> criados,
        Dictionary<string, int> ignorados,
        CancellationToken ct)
    {
        bool existe = await parametroSistemaRepo.ExisteParaTenantAsync(tenant.Id, ct);

        if (existe)
        {
            criados["parametrosSistema"] = 0;
            ignorados["parametrosSistema"] = 1;
        }
        else
        {
            ParametroSistema parametro = ParametroSistema.CriarParaTenant(tenant.Id, clock);
            parametroSistemaRepo.Add(parametro);
            criados["parametrosSistema"] = 1;
            ignorados["parametrosSistema"] = 0;
        }
    }

    private async Task SeedParametroCotacaoAsync(
        Tenant tenant,
        Dictionary<string, int> criados,
        Dictionary<string, int> ignorados,
        CancellationToken ct)
    {
        bool existe = await parametroCotacaoRepo.ExisteParaTenantAsync(tenant.Id, ct);

        if (existe)
        {
            criados["parametrosCotacao"] = 0;
            ignorados["parametrosCotacao"] = 1;
        }
        else
        {
            ParametroCotacao parametro = ParametroCotacao.CriarDefault(tenant.Id, clock);
            parametroCotacaoRepo.Add(parametro);
            criados["parametrosCotacao"] = 1;
            ignorados["parametrosCotacao"] = 0;
        }
    }

    // Seed de PlanoContas postergado para Task -1.10 — PlanoContasModelo não existe ainda.
    private void SeedPlanoContas(Dictionary<string, int> criados, Dictionary<string, int> ignorados)
    {
        criados["planoContas"] = 0;
        ignorados["planoContas"] = 0;
        LogSeedPlanoContasPendente(logger);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Tenant '{Slug}' provisionado — parametros_sistema: {ParametrosSistemaCriados}, parametros_cotacao: {ParametrosCotacaoCriados}")]
    private static partial void LogProvisionamentoConcluido(
        ILogger logger,
        string slug,
        int parametrosSistemaCriados,
        int parametrosCotacaoCriados);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Seed de PlanoContas pendente (Task -1.10 — PlanoContasModelo não existe ainda).")]
    private static partial void LogSeedPlanoContasPendente(ILogger logger);
}
