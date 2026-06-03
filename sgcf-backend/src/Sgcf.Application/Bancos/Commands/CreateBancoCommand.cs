using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Bancos;

namespace Sgcf.Application.Bancos.Commands;

public sealed record CreateBancoCommand(
    string CodigoCompe,
    string RazaoSocial,
    string Apelido)
    : IRequest<BancoDto>
{
    /// <summary>
    /// Regime de limite do banco ("PerModalidade" | "GlobalPuro"). Quando omitido, o banco
    /// nasce em PerModalidade. SPEC_REGIME_LIMITE_EXPLICITO §5.1.
    /// </summary>
    public string? RegimeLimite { get; init; }
}

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

        RuleFor(c => c.RegimeLimite)
            .Must(v => Enum.TryParse<RegimeLimiteBanco>(v!, ignoreCase: true, out _))
            .WithMessage($"RegimeLimite deve ser um dos valores: {string.Join(", ", Enum.GetNames<RegimeLimiteBanco>())}.")
            .When(c => c.RegimeLimite is not null);
    }
}

public sealed class CreateBancoCommandHandler(IBancoRepository repo, IClock clock)
    : IRequestHandler<CreateBancoCommand, BancoDto>
{
    public async Task<BancoDto> Handle(CreateBancoCommand cmd, CancellationToken cancellationToken)
    {
        Banco banco = Banco.Criar(cmd.CodigoCompe, cmd.RazaoSocial, cmd.Apelido, clock);

        if (cmd.RegimeLimite is not null)
        {
            RegimeLimiteBanco regime = Enum.Parse<RegimeLimiteBanco>(cmd.RegimeLimite, ignoreCase: true);
            if (regime != RegimeLimiteBanco.PerModalidade)
            {
                banco.DefinirRegimeLimite(regime, clock);
            }
        }

        repo.Add(banco);
        await repo.SaveChangesAsync(cancellationToken);
        return BancoDto.From(banco);
    }
}
