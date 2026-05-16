using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio.Commands;

public sealed record UpdateParametroCommand(
    Guid Id,
    string TipoCotacao,
    bool Ativo)
    : IRequest<ParametroCotacaoDto>;

public sealed class UpdateParametroCommandValidator : AbstractValidator<UpdateParametroCommand>
{
    public UpdateParametroCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.TipoCotacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<Domain.Cambio.TipoCotacao>(v, true, out _))
            .WithMessage($"TipoCotacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Cambio.TipoCotacao>())}.");
    }
}

public sealed class UpdateParametroCommandHandler(IParametroCotacaoRepository repo, IClock clock)
    : IRequestHandler<UpdateParametroCommand, ParametroCotacaoDto>
{
    public async Task<ParametroCotacaoDto> Handle(UpdateParametroCommand cmd, CancellationToken cancellationToken)
    {
        ParametroCotacao parametro = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ParametroCotacao com Id '{cmd.Id}' não encontrado.");

        Domain.Cambio.TipoCotacao tipoCotacao = Enum.Parse<Domain.Cambio.TipoCotacao>(cmd.TipoCotacao, true);
        parametro.Atualizar(tipoCotacao, cmd.Ativo, clock);
        await repo.SaveChangesAsync(cancellationToken);
        return ParametroCotacaoDto.From(parametro);
    }
}
