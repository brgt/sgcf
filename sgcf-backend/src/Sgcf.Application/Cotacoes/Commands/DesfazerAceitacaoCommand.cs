using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Transição: Aceita → Comparada (apenas se ainda não convertida). SPEC §4.1.</summary>
public sealed record DesfazerAceitacaoCommand(Guid CotacaoId) : IRequest<Unit>;

public sealed class DesfazerAceitacaoCommandValidator : AbstractValidator<DesfazerAceitacaoCommand>
{
    public DesfazerAceitacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty();
    }
}

public sealed class DesfazerAceitacaoCommandHandler(ICotacaoRepository repo, IClock clock)
    : IRequestHandler<DesfazerAceitacaoCommand, Unit>
{
    public async Task<Unit> Handle(DesfazerAceitacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        cotacao.DesfazerAceitacao(clock);
        await repo.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
