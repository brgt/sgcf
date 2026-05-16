using Sgcf.Domain.Common;

namespace Sgcf.Application.Cambio;

public interface ICotacaoSpotCache
{
    public Task<Money?> GetSpotAsync(Moeda moeda, CancellationToken cancellationToken = default);
}
