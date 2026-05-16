using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Application.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>
/// Aceita uma proposta. Registra AceitaPor a partir do <see cref="ICurrentUserService"/>.
/// SPEC §3.2 regra 6, §4.1, §4.2.
/// </summary>
public sealed record AceitarPropostaCommand(
    Guid CotacaoId,
    Guid PropostaId) : IRequest<Unit>;

public sealed class AceitarPropostaCommandValidator : AbstractValidator<AceitarPropostaCommand>
{
    public AceitarPropostaCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
        RuleFor(c => c.PropostaId).NotEmpty();
    }
}

public sealed class AceitarPropostaCommandHandler(
    ICotacaoRepository repo,
    ICurrentUserService currentUser,
    IClock clock) : IRequestHandler<AceitarPropostaCommand, Unit>
{
    public async Task<Unit> Handle(AceitarPropostaCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        // actorSub vem do contexto HTTP via ICurrentUserService
        string actorSub = currentUser.ActorSub;

        cotacao.AceitarProposta(cmd.PropostaId, actorSub, clock);
        await repo.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
