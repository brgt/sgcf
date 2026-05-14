using FluentValidation;
using MediatR;
using Sgcf.Application.Common;

namespace Sgcf.Application.Auditoria.Queries;

/// <summary>
/// Query paginada de eventos de auditoria. Todos os filtros são opcionais.
/// </summary>
public sealed record ListAuditEventosQuery(
    string? Entity = null,
    Guid? EntityId = null,
    string? ActorSub = null,
    string? Source = null,
    string? Operation = null,
    DateTimeOffset? De = null,
    DateTimeOffset? Ate = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<PagedResult<AuditEventoDto>>;

public sealed class ListAuditEventosQueryValidator : AbstractValidator<ListAuditEventosQuery>
{
    public ListAuditEventosQueryValidator()
    {
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

public sealed class ListAuditEventosQueryHandler(IAuditLogRepository repo)
    : IRequestHandler<ListAuditEventosQuery, PagedResult<AuditEventoDto>>
{
    public async Task<PagedResult<AuditEventoDto>> Handle(
        ListAuditEventosQuery query,
        CancellationToken cancellationToken)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 200);

        AuditFilter filter = new(
            Entity: query.Entity,
            EntityId: query.EntityId,
            ActorSub: query.ActorSub,
            Source: query.Source,
            Operation: query.Operation,
            De: query.De,
            Ate: query.Ate,
            Page: page,
            PageSize: pageSize);

        return await repo.ListAsync(filter, cancellationToken);
    }
}
