using Sgcf.Domain.Common;

namespace Sgcf.Application.Cotacoes;

public interface ICotacaoSpotCache
{
    public Task<Money?> GetSpotAsync(Moeda moeda, CancellationToken cancellationToken = default);
}
