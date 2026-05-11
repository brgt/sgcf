using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

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
            .Must(v => Enum.TryParse<Domain.Cotacoes.TipoCotacao>(v, true, out _))
            .WithMessage($"TipoCotacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Cotacoes.TipoCotacao>())}.");
    }
}

public sealed class UpdateParametroCommandHandler(IParametroCotacaoRepository repo, IClock clock)
    : IRequestHandler<UpdateParametroCommand, ParametroCotacaoDto>
{
    public async Task<ParametroCotacaoDto> Handle(UpdateParametroCommand cmd, CancellationToken cancellationToken)
    {
        ParametroCotacao parametro = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ParametroCotacao com Id '{cmd.Id}' não encontrado.");

        Domain.Cotacoes.TipoCotacao tipoCotacao = Enum.Parse<Domain.Cotacoes.TipoCotacao>(cmd.TipoCotacao, true);
        parametro.Atualizar(tipoCotacao, cmd.Ativo, clock);
        await repo.SaveChangesAsync(cancellationToken);
        return ParametroCotacaoDto.From(parametro);
    }
}
