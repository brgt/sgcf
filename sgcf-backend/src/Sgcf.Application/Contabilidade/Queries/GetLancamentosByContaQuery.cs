using MediatR;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Queries;

public sealed record GetLancamentosByContaQuery(Guid ContratoId)
    : IRequest<IReadOnlyList<LancamentoContabilDto>>;

public sealed class GetLancamentosByContaQueryHandler(ILancamentoContabilRepository repo)
    : IRequestHandler<GetLancamentosByContaQuery, IReadOnlyList<LancamentoContabilDto>>
{
    public async Task<IReadOnlyList<LancamentoContabilDto>> Handle(
        GetLancamentosByContaQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LancamentoContabil> lancamentos =
            await repo.ListByContratoAsync(query.ContratoId, cancellationToken);

        List<LancamentoContabilDto> result = new(lancamentos.Count);

        foreach (LancamentoContabil lancamento in lancamentos)
        {
            result.Add(LancamentoContabilDto.From(lancamento));
        }

        return result.AsReadOnly();
    }
}
