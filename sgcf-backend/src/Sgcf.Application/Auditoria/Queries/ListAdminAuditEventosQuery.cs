using FluentValidation;
using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Auditoria.Queries;

/// <summary>
/// Query admin para listar eventos de auditoria de um tenant específico.
/// Ignora o global filter de tenant — uso exclusivo de super-admin.
/// </summary>
public sealed record ListAdminAuditEventosQuery(
    Guid TenantId,
    string? Entity = null,
    Guid? EntityId = null,
    string? ActorSub = null,
    string? Source = null,
    string? Operation = null,
    DateTimeOffset? De = null,
    DateTimeOffset? Ate = null,
    bool? Impersonating = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<PagedResult<AuditLogDto>>;

public sealed class ListAdminAuditEventosQueryValidator : AbstractValidator<ListAdminAuditEventosQuery>
{
    public ListAdminAuditEventosQueryValidator()
    {
        RuleFor(q => q.TenantId)
            .NotEmpty()
            .WithMessage("tenantId é obrigatório.");

        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page deve ser maior ou igual a 1.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("PageSize deve estar entre 1 e 200.");

        RuleFor(q => q.Ate)
            .GreaterThanOrEqualTo(q => q.De)
            .When(q => q.De.HasValue && q.Ate.HasValue)
            .WithMessage("Ate deve ser maior ou igual a De.");
    }
}

public sealed class ListAdminAuditEventosQueryHandler(IAuditLogRepository repo)
    : IRequestHandler<ListAdminAuditEventosQuery, PagedResult<AuditLogDto>>
{
    public async Task<PagedResult<AuditLogDto>> Handle(
        ListAdminAuditEventosQuery query,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 200);

        AuditFilter filter = new(
            Entity:         query.Entity,
            EntityId:       query.EntityId,
            ActorSub:       query.ActorSub,
            Source:         query.Source,
            Operation:      query.Operation,
            De:             query.De,
            Ate:            query.Ate,
            Impersonating:  query.Impersonating,
            Page:           page,
            PageSize:       pageSize);

        return await repo.ListForTenantAsync(query.TenantId, filter, cancellationToken);
    }
}
