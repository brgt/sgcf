using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Commands;

public sealed record CreateContaCommand(
    string CodigoGerencial,
    string Nome,
    string Natureza)
    : IRequest<PlanoContasDto>;

public sealed class CreateContaCommandValidator : AbstractValidator<CreateContaCommand>
{
    public CreateContaCommandValidator()
    {
        RuleFor(c => c.CodigoGerencial)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage("CodigoGerencial não pode ser vazio e deve ter no máximo 20 caracteres.");

        RuleFor(c => c.Nome)
            .NotEmpty()
            .WithMessage("Nome não pode ser vazio.");

        RuleFor(c => c.Natureza)
            .NotEmpty()
            .Must(v => Enum.TryParse<NaturezaConta>(v, true, out _))
            .WithMessage($"Natureza deve ser um dos valores: {string.Join(", ", Enum.GetNames<NaturezaConta>())}.");
    }
}

public sealed class CreateContaCommandHandler(IPlanoContasRepository repo, IClock clock)
    : IRequestHandler<CreateContaCommand, PlanoContasDto>
{
    public async Task<PlanoContasDto> Handle(CreateContaCommand cmd, CancellationToken cancellationToken)
    {
        NaturezaConta natureza = Enum.Parse<NaturezaConta>(cmd.Natureza, true);
        PlanoContasGerencial conta = PlanoContasGerencial.Criar(cmd.CodigoGerencial, cmd.Nome, natureza, clock);
        repo.Add(conta);
        await repo.SaveChangesAsync(cancellationToken);
        return PlanoContasDto.From(conta);
    }
}
