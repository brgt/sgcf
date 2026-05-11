using NodaTime;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

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
}
