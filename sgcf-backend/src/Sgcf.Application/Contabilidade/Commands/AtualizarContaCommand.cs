using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Commands;

public sealed record AtualizarContaCommand(
    Guid Id,
    string Nome,
    string Natureza,
    string? CodigoSapB1)
    : IRequest<PlanoContasDto>;

public sealed class AtualizarContaCommandValidator : AbstractValidator<AtualizarContaCommand>
{
    public AtualizarContaCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Nome)
            .NotEmpty()
            .WithMessage("Nome não pode ser vazio.");

        RuleFor(c => c.Natureza)
            .NotEmpty()
            .Must(v => Enum.TryParse<NaturezaConta>(v, true, out _))
            .WithMessage($"Natureza deve ser um dos valores: {string.Join(", ", Enum.GetNames<NaturezaConta>())}.");
    }
}

public sealed class AtualizarContaCommandHandler(IPlanoContasRepository repo, IClock clock)
    : IRequestHandler<AtualizarContaCommand, PlanoContasDto>
{
    public async Task<PlanoContasDto> Handle(AtualizarContaCommand cmd, CancellationToken cancellationToken)
    {
        PlanoContasGerencial conta = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"PlanoContasGerencial com Id '{cmd.Id}' não encontrado.");

        NaturezaConta natureza = Enum.Parse<NaturezaConta>(cmd.Natureza, true);
        conta.Atualizar(cmd.Nome, natureza, cmd.CodigoSapB1, clock);
        await repo.SaveChangesAsync(cancellationToken);
        return PlanoContasDto.From(conta);
    }
}
