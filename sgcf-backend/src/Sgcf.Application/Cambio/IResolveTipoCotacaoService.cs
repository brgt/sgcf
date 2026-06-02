using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cambio;

namespace Sgcf.Application.Cambio;

public sealed record ResultadoCotacao(
    Money ValorMidRate,
    TipoCotacao Tipo,
    Instant Momento
);

public interface IResolveTipoCotacaoService
{
    // Returns null if no cotacao found in DB for the requested moeda
    public Task<ResultadoCotacao?> ResolveAsync(
        Moeda moeda,
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a <see cref="CotacaoFx"/> bruta para um tipo lógico em uma data de referência,
    /// aplicando a tradução de armazenamento <c>PtaxD1 → PtaxD0(D-1)</c> — pois o ingestor do BCB
    /// grava apenas <c>PtaxD0</c>/<c>SpotIntraday</c>, nunca <c>PtaxD1</c>.
    /// Para tipos diferentes de <c>PtaxD1</c>, consulta o próprio tipo em <paramref name="dataReferencia"/>
    /// (sem deslocamento de data).
    /// Retorna <c>null</c> se não houver cotação correspondente.
    /// O chamador escolhe qual valor usar (compra, venda ou mid) a partir da entidade retornada.
    /// </summary>
    public Task<CotacaoFx?> ResolverFxAsync(
        Moeda moeda,
        TipoCotacao tipoLogico,
        LocalDate dataReferencia,
        CancellationToken cancellationToken = default);
}
