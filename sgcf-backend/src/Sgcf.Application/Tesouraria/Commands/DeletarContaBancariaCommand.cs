using MediatR;
using NodaTime;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Commands;

/// <summary>
/// Realiza o soft delete de uma conta bancária.
/// Após a deleção, a conta não aparece mais nas queries padrão (query filter de DeletedAt).
/// </summary>
public sealed record DeletarContaBancariaCommand(Guid Id) : IRequest;

public sealed class DeletarContaBancariaCommandHandler(
    IContaBancariaRepository repo,
    IClock clock)
    : IRequestHandler<DeletarContaBancariaCommand>
{
    public async Task Handle(
        DeletarContaBancariaCommand cmd,
        CancellationToken cancellationToken)
    {
        ContaBancaria conta = await repo.GetByIdAsync(cmd.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ContaBancaria {cmd.Id} não encontrada.");

        conta.Deletar(clock);

        await repo.SaveChangesAsync(cancellationToken);
    }
}
