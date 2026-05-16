using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Cotacoes.Queries;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Cadastra snapshot de CDI diário. Admin cadastra manualmente no MVP. SPEC §13 decisão 2.
/// </summary>
public sealed record CreateCdiSnapshotCommand(
    DateOnly Data,
    decimal CdiAaPercentual) : IRequest<CdiSnapshotDto>;

public sealed class CreateCdiSnapshotCommandValidator : AbstractValidator<CreateCdiSnapshotCommand>
{
    public CreateCdiSnapshotCommandValidator()
    {
        RuleFor(c => c.CdiAaPercentual)
            .GreaterThan(0m)
            .WithMessage("CdiAaPercentual deve ser maior que zero.");
    }
}

public sealed class CreateCdiSnapshotCommandHandler(ICdiSnapshotRepository repo, IClock clock)
    : IRequestHandler<CreateCdiSnapshotCommand, CdiSnapshotDto>
{
    public async Task<CdiSnapshotDto> Handle(CreateCdiSnapshotCommand cmd, CancellationToken cancellationToken)
    {
        LocalDate data = new(cmd.Data.Year, cmd.Data.Month, cmd.Data.Day);

        CdiSnapshot? existente = await repo.GetByDataAsync(data, cancellationToken);
        if (existente is not null)
        {
            throw new InvalidOperationException($"CDI já cadastrado para a data {data}.");
        }

        CdiSnapshot snapshot = CdiSnapshot.Criar(data, cmd.CdiAaPercentual, clock);
        repo.Add(snapshot);
        await repo.SaveChangesAsync(cancellationToken);

        return CdiSnapshotDto.From(snapshot);
    }
}
