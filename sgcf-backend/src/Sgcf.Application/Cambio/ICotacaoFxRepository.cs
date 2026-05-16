using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio;

public interface ICotacaoFxRepository
{
    public Task UpsertAsync(CotacaoFx cotacao, CancellationToken cancellationToken = default);

    // Returns most recent CotacaoFx with matching moeda+tipo where Momento is on or before dataMaxima (UTC date boundary)
    public Task<CotacaoFx?> GetMaisRecenteAsync(Moeda moeda, TipoCotacao tipo, LocalDate dataMaxima, CancellationToken cancellationToken = default);
}
