using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Bancos;
using Sgcf.Domain.Common;

namespace Sgcf.Application.Bancos.Commands;

public sealed record CreateBancoCommand(
    string CodigoCompe,
    string RazaoSocial,
    string Apelido,
    string PadraoAntecipacao)
    : IRequest<BancoDto>;

public sealed class CreateBancoCommandValidator : AbstractValidator<CreateBancoCommand>
{
    public CreateBancoCommandValidator()
    {
        RuleFor(c => c.CodigoCompe)
            .NotEmpty()
            .Length(3)
            .WithMessage("CodigoCompe deve ter exatamente 3 caracteres.");

        RuleFor(c => c.RazaoSocial)
            .NotEmpty()
            .WithMessage("RazaoSocial não pode ser vazia.");

        RuleFor(c => c.Apelido)
            .NotEmpty()
            .WithMessage("Apelido não pode ser vazio.");

        RuleFor(c => c.PadraoAntecipacao)
            .NotEmpty()
            .Must(v => Enum.TryParse<Domain.Common.PadraoAntecipacao>(v, true, out _))
            .WithMessage($"PadraoAntecipacao deve ser um dos valores: {string.Join(", ", Enum.GetNames<Domain.Common.PadraoAntecipacao>())}.");
    }
}

public sealed class CreateBancoCommandHandler(IBancoRepository repo, IClock clock)
    : IRequestHandler<CreateBancoCommand, BancoDto>
{
    public async Task<BancoDto> Handle(CreateBancoCommand cmd, CancellationToken cancellationToken)
    {
        Domain.Common.PadraoAntecipacao padrao = Enum.Parse<Domain.Common.PadraoAntecipacao>(cmd.PadraoAntecipacao, true);
        Banco banco = Banco.Criar(cmd.CodigoCompe, cmd.RazaoSocial, cmd.Apelido, padrao, clock);
        repo.Add(banco);
        await repo.SaveChangesAsync(cancellationToken);
        return BancoDto.From(banco);
    }
}
