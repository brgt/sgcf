using FluentValidation;
using MediatR;
using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Commands;

/// <summary>Atualiza valor do limite operacional. SPEC §6.1.</summary>
public sealed record UpdateLimiteBancoCommand(
    Guid LimiteId,
    decimal NovoValorLimiteBrl) : IRequest<LimiteBancoDto>;

public sealed class UpdateLimiteBancoCommandValidator : AbstractValidator<UpdateLimiteBancoCommand>
{
    public UpdateLimiteBancoCommandValidator()
    {
        RuleFor(c => c.LimiteId).NotEmpty();
        RuleFor(c => c.NovoValorLimiteBrl).GreaterThan(0m).WithMessage("NovoValorLimiteBrl deve ser maior que zero.");
    }
}

public sealed class UpdateLimiteBancoCommandHandler(ILimiteBancoRepository repo, IClock clock)
    : IRequestHandler<UpdateLimiteBancoCommand, LimiteBancoDto>
{
    public async Task<LimiteBancoDto> Handle(UpdateLimiteBancoCommand cmd, CancellationToken cancellationToken)
    {
        LimiteBanco limite = await repo.GetByIdAsync(cmd.LimiteId, cancellationToken)
            ?? throw new KeyNotFoundException($"Limite '{cmd.LimiteId}' não encontrado.");

        Money novoValor = new(cmd.NovoValorLimiteBrl, Moeda.Brl);
        limite.Atualizar(clock, novoValor);
        repo.Update(limite);
        await repo.SaveChangesAsync(cancellationToken);

        return LimiteBancoDto.From(limite);
    }
}
