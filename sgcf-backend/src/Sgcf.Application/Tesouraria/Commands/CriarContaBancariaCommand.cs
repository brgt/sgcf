using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Commands;

/// <summary>
/// Cria uma nova conta bancária para o tenant corrente.
/// O <c>TenantId</c> é preenchido automaticamente pelo <c>TenantSaveInterceptor</c>.
/// </summary>
public sealed record CriarContaBancariaCommand(
    Guid BancoId,
    string Nome,
    string Agencia,
    string NumeroConta,
    Moeda Moeda)
    : IRequest<ContaBancariaDto>;

public sealed class CriarContaBancariaCommandValidator : AbstractValidator<CriarContaBancariaCommand>
{
    public CriarContaBancariaCommandValidator()
    {
        RuleFor(c => c.BancoId)
            .NotEmpty()
            .WithMessage("BancoId é obrigatório.");

        RuleFor(c => c.Nome)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Nome é obrigatório e deve ter no máximo 200 caracteres.");

        RuleFor(c => c.Agencia)
            .NotEmpty()
            .MaximumLength(10)
            .WithMessage("Agencia é obrigatória e deve ter no máximo 10 caracteres.");

        RuleFor(c => c.NumeroConta)
            .NotEmpty()
            .MaximumLength(20)
            .WithMessage("NumeroConta é obrigatório e deve ter no máximo 20 caracteres.");

        RuleFor(c => c.Moeda)
            .IsInEnum()
            .WithMessage("Moeda inválida.");
    }
}

public sealed class CriarContaBancariaCommandHandler(
    IContaBancariaRepository repo,
    IClock clock)
    : IRequestHandler<CriarContaBancariaCommand, ContaBancariaDto>
{
    public async Task<ContaBancariaDto> Handle(
        CriarContaBancariaCommand cmd,
        CancellationToken cancellationToken)
    {
        ContaBancaria conta = ContaBancaria.Criar(
            cmd.BancoId,
            cmd.Nome,
            cmd.Agencia,
            cmd.NumeroConta,
            cmd.Moeda,
            clock);

        await repo.AddAsync(conta, cancellationToken);
        await repo.SaveChangesAsync(cancellationToken);

        return ContaBancariaDto.From(conta);
    }
}
