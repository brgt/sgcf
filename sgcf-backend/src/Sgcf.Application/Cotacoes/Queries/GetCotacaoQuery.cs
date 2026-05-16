using MediatR;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes.Queries;

/// <summary>Detalhe de cotação com todas as propostas. SPEC §6.2.</summary>
public sealed record GetCotacaoQuery(Guid Id) : IRequest<CotacaoDto>;

public sealed class GetCotacaoQueryHandler(ICotacaoRepository repo)
    : IRequestHandler<GetCotacaoQuery, CotacaoDto>
{
    public async Task<CotacaoDto> Handle(GetCotacaoQuery query, CancellationToken cancellationToken)
    {
        Cotacao cotacao = await repo.GetByIdWithPropostasAsync(query.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Cotação '{query.Id}' não encontrada.");

        return CotacaoDto.From(cotacao);
    }
}
