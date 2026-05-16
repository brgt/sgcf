using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Cancela cotação (→ Recusada). SPEC §4.1.</summary>
public sealed record CancelarCotacaoCommand(
    Guid CotacaoId,
    string Motivo) : IRequest<Unit>;

public sealed class CancelarCotacaoCommandValidator : AbstractValidator<CancelarCotacaoCommand>
{
    public CancelarCotacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
        RuleFor(c => c.Motivo).NotEmpty().WithMessage("Motivo é obrigatório para cancelamento.");
    }
}

public sealed class CancelarCotacaoCommandHandler(ICotacaoRepository repo, IClock clock)
    : IRequestHandler<CancelarCotacaoCommand, Unit>
{
    public async Task<Unit> Handle(CancelarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        cotacao.Cancelar(cmd.Motivo, clock);
        await repo.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
