using FluentValidation;
using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Remove banco-alvo da cotação. Apenas em Rascunho ou EmCaptacao. SPEC §6.1.</summary>
public sealed record RemoverBancoDaCotacaoCommand(
    Guid CotacaoId,
    Guid BancoId) : IRequest<Unit>;

public sealed class RemoverBancoDaCotacaoCommandValidator : AbstractValidator<RemoverBancoDaCotacaoCommand>
{
    public RemoverBancoDaCotacaoCommandValidator()
    {
        RuleFor(c => c.CotacaoId).NotEmpty().WithMessage("CotacaoId não pode ser vazio.");
        RuleFor(c => c.BancoId).NotEmpty().WithMessage("BancoId não pode ser vazio.");
    }
}

public sealed class RemoverBancoDaCotacaoCommandHandler(ICotacaoRepository repo)
    : IRequestHandler<RemoverBancoDaCotacaoCommand, Unit>
{
    public async Task<Unit> Handle(RemoverBancoDaCotacaoCommand cmd, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdAsync(cmd.CotacaoId, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{cmd.CotacaoId}' não encontrada.");

        cotacao.RemoverBancoAlvo(cmd.BancoId);
        await repo.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
