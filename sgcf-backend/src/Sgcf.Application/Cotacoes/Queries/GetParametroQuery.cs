using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

public sealed record GetParametroQuery(Guid Id) : IRequest<ParametroCotacaoDto>;

public sealed class GetParametroQueryHandler(IParametroCotacaoRepository repo)
    : IRequestHandler<GetParametroQuery, ParametroCotacaoDto>
{
    public async Task<ParametroCotacaoDto> Handle(GetParametroQuery query, CancellationToken cancellationToken)
    {
        ParametroCotacao parametro = await repo.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"ParametroCotacao com Id '{query.Id}' não encontrado.");

        return ParametroCotacaoDto.From(parametro);
    }
}
