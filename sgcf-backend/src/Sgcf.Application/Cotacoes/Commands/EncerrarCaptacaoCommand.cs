using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Transição: EmCaptacao → Comparada. SPEC §4.1.</summary>
public sealed record EncerrarCaptacaoCommand(Guid CotacaoId) : IRequest<Unit>;

public sealed class EncerrarCaptacaoCommandValidator : AbstractValidator<EncerrarCaptacaoCommand>
{
    public EncerrarCaptacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty().WithMessage("CotacaoId não pode ser vazio.");
    }
}

public sealed class EncerrarCaptacaoCommandHandler(ICotacaoRepository repo, IClock clock)
    : IRequestHandler<EncerrarCaptacaoCommand, Unit>
{
    public async Task<Unit> Handle(EncerrarCaptacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        cotacao.EncerrarCaptacao(clock);
        await repo.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
