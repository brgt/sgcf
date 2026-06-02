using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio;

public interface ICotacaoFxRepository
{
    public Task UpsertAsync(CotacaoFx cotacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra ou <b>atualiza</b> uma cotação pela chave única
    /// (moeda base, moeda quote, momento, tipo): insere se não existir; caso exista,
    /// atualiza compra/venda/fonte. Usado no cadastro/correção manual (endpoint admin),
    /// onde re-enviar a mesma chave deve corrigir o valor — diferente de
    /// <see cref="UpsertAsync"/> (insert-if-not-exists, usado pelo ingestor).
    /// </summary>
    public Task RegistrarOuAtualizarAsync(CotacaoFx cotacao, CancellationToken cancellationToken = default);

    // Returns most recent CotacaoFx with matching moeda+tipo where Momento is on or before dataMaxima (UTC date boundary)
    public Task<CotacaoFx?> GetMaisRecenteAsync(Moeda moeda, TipoCotacao tipo, LocalDate dataMaxima, CancellationToken cancellationToken = default);
}
