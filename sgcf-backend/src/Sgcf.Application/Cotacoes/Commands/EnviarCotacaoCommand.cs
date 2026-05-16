using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Transição: Rascunho → EmCaptacao. SPEC §4.1.</summary>
public sealed record EnviarCotacaoCommand(Guid CotacaoId) : IRequest<Unit>;

public sealed class EnviarCotacaoCommandValidator : AbstractValidator<EnviarCotacaoCommand>
{
    public EnviarCotacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty().WithMessage("CotacaoId não pode ser vazio.");
    }
}

public sealed class EnviarCotacaoCommandHandler(ICotacaoRepository repo, IClock clock)
    : IRequestHandler<EnviarCotacaoCommand, Unit>
{
    public async Task<Unit> Handle(EnviarCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        cotacao.Enviar(clock);
        await repo.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
