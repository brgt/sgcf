using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Commands;

public sealed record CreateBancoCommand(
    string CodigoCompe,
    string RazaoSocial,
    string Apelido)
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
    }
}

public sealed class CreateBancoCommandHandler(IBancoRepository repo, IClock clock)
    : IRequestHandler<CreateBancoCommand, BancoDto>
{
    public async Task<BancoDto> Handle(CreateBancoCommand cmd, CancellationToken cancellationToken)
    {
        Banco banco = Banco.Criar(cmd.CodigoCompe, cmd.RazaoSocial, cmd.Apelido, clock);
        repo.Add(banco);
        await repo.SaveChangesAsync(cancellationToken);
        return BancoDto.From(banco);
    }
}
