using MediatR;
using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade.Queries;

/// <summary>
/// Lista todas as entradas do modelo global de plano de contas.
/// Acesso restrito a super-admin — o modelo é global e não tenant-scoped.
/// </summary>
public sealed record ListContasModeloQuery : IRequest<IReadOnlyList<PlanoContasModeloDto>>;

public sealed class ListContasModeloQueryHandler(IPlanoContasModeloRepository repo)
    : IRequestHandler<ListContasModeloQuery, IReadOnlyList<PlanoContasModeloDto>>
{
    public async Task<IReadOnlyList<PlanoContasModeloDto>> Handle(
        ListContasModeloQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PlanoContasModelo> contas = await repo.ListAllAsync(cancellationToken);
        List<PlanoContasModeloDto> result = new(contas.Count);

        foreach (PlanoContasModelo conta in contas)
        {
            result.Add(PlanoContasModeloDto.From(conta));
        }

        return result.AsReadOnly();
    }
}
