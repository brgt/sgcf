using MediatR;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Queries;

public sealed record GetContaQuery(Guid Id) : IRequest<PlanoContasDto>;

public sealed class GetContaQueryHandler(IPlanoContasRepository repo)
    : IRequestHandler<GetContaQuery, PlanoContasDto>
{
    public async Task<PlanoContasDto> Handle(GetContaQuery query, CancellationToken cancellationToken)
    {
        PlanoContasGerencial conta = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"PlanoContasGerencial com Id '{query.Id}' não encontrado.");

        return PlanoContasDto.From(conta);
    }
}
