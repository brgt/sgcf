using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Commands;

/// <summary>
/// Atualiza os campos mutáveis de uma conta bancária existente.
/// </summary>
public sealed record AtualizarContaBancariaCommand(
    Guid Id,
    string Nome,
    string Agencia,
    string NumeroConta,
    Moeda Moeda)
    : IRequest<ContaBancariaDto>;

public sealed class AtualizarContaBancariaCommandValidator : AbstractValidator<AtualizarContaBancariaCommand>
{
    public AtualizarContaBancariaCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty()
            .WithMessage("Id é obrigatório.");

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

public sealed class AtualizarContaBancariaCommandHandler(
    IContaBancariaRepository repo,
    IClock clock)
    : IRequestHandler<AtualizarContaBancariaCommand, ContaBancariaDto>
{
    public async Task<ContaBancariaDto> Handle(
        AtualizarContaBancariaCommand cmd,
        CancellationToken cancellationToken)
    {
        ContaBancaria conta = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ContaBancaria {cmd.Id} não encontrada.");

        conta.Atualizar(cmd.Nome, cmd.Agencia, cmd.NumeroConta, cmd.Moeda, clock);

        await repo.SaveChangesAsync(cancellationToken);

        return ContaBancariaDto.From(conta);
    }
}
