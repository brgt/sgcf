using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio.Commands;

public sealed record CreateParametroCommand(
    Guid? BancoId,
    string? Modalidade,
    string TipoCotacao)
    : IRequest<ParametroCotacaoDto>;

public sealed class CreateParametroCommandValidator : AbstractValidator<CreateParametroCommand>
{
    public CreateParametroCommandValidator()
    {
        RuleFor(c => c.TipoCotacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<Domain.Cambio.TipoCotacao>(v, true, out _))
            .WithMessage($"TipoCotacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Cambio.TipoCotacao>())}.");

        RuleFor(c => c.Modalidade)
            .Must(v => v == null || Enum.TryParse<ModalidadeContrato>(v, true, out _))
            .WithMessage($"Modalidade deve ser null ou um dos valores: {string.Join(", ", Enum.GetNames<ModalidadeContrato>())}.");
    }
}

public sealed class CreateParametroCommandHandler(IParametroCotacaoRepository repo, IClock clock)
    : IRequestHandler<CreateParametroCommand, ParametroCotacaoDto>
{
    public async Task<ParametroCotacaoDto> Handle(CreateParametroCommand cmd, CancellationToken cancellationToken)
    {
        Domain.Cambio.TipoCotacao tipoCotacao = Enum.Parse<Domain.Cambio.TipoCotacao>(cmd.TipoCotacao, true);

        ModalidadeContrato? modalidade = cmd.Modalidade == null
            ? (ModalidadeContrato?)null
            : Enum.Parse<ModalidadeContrato>(cmd.Modalidade, true);

        ParametroCotacao parametro = ParametroCotacao.Criar(cmd.BancoId, modalidade, tipoCotacao, clock);
        repo.Add(parametro);
        await repo.SaveChangesAsync(cancellationToken);
        return ParametroCotacaoDto.From(parametro);
    }
}
