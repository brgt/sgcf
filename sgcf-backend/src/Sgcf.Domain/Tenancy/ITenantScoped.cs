namespace Sgcf.Domain.Tenancy;

/// <summary>
/// Marker interface que identifica entidades cujos dados pertencem a um tenant específico.
/// Entidades que implementam esta interface recebem:
/// 1. Coluna <c>tenant_id UUID NOT NULL</c> no banco de dados.
/// 2. EF Core Global Query Filter — queries automáticas filtram pelo tenant corrente.
/// 3. <c>TenantSaveInterceptor</c> — preenche <c>TenantId</c> automaticamente no INSERT.
///
/// NÃO implementar em catálogos globais: <c>Banco</c>, <c>Feriado</c>, <c>CotacaoFx</c>, <c>CdiSnapshot</c>.
/// </summary>
public interface ITenantScoped
{
    public Guid TenantId { get; }
}
