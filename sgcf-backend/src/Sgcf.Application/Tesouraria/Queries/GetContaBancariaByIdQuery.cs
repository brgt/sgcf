using MediatR;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria.Queries;

/// <summary>
/// Retorna uma conta bancária pelo identificador.
/// Lança <see cref="KeyNotFoundException"/> quando não encontrada.
/// </summary>
public sealed record GetContaBancariaByIdQuery(Guid Id) : IRequest<ContaBancariaDto>;

public sealed class GetContaBancariaByIdQueryHandler(IContaBancariaRepository repo)
    : IRequestHandler<GetContaBancariaByIdQuery, ContaBancariaDto>
{
    public async Task<ContaBancariaDto> Handle(
        GetContaBancariaByIdQuery query,
        CancellationToken cancellationToken)
    {
        ContaBancaria conta = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ContaBancaria {query.Id} não encontrada.");

        return ContaBancariaDto.From(conta);
    }
}
