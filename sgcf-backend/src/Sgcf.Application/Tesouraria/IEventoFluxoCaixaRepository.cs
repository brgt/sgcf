using NodaTime;
using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Porta de persistência para <see cref="EventoFluxoCaixa"/>.
/// O EF global filter isola automaticamente por tenant_id.
/// </summary>
public interface IEventoFluxoCaixaRepository
{
    /// <summary>
    /// Lista os eventos de fluxo de caixa no intervalo de datas informado (inclusivo),
    /// ordenados por <c>Data</c> ascendente.
    /// </summary>
    public Task<IReadOnlyList<EventoFluxoCaixa>> ListByPeriodoAsync(
        LocalDate dataDe,
        LocalDate dataAte,
        CancellationToken ct = default);

    /// <summary>Adiciona um novo evento ao contexto (sem persistir — use <see cref="SaveChangesAsync"/>).</summary>
    public Task AddAsync(EventoFluxoCaixa evento, CancellationToken ct = default);

    /// <summary>Persiste as alterações pendentes no contexto EF.</summary>
    public Task SaveChangesAsync(CancellationToken ct = default);
}
