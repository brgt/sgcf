using NodaTime;
using Sgcf.Domain.Cronograma;

namespace Sgcf.Application.Contratos;

public interface IEventoCronogramaRepository
{
    public Task<IReadOnlyList<EventoCronograma>> GetByContratoIdAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public void Add(EventoCronograma evento);
    public void AddRange(IEnumerable<EventoCronograma> eventos);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    public Task<bool> ExistsForContratoAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task DeleteAllByContratoIdAsync(Guid contratoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todos os eventos com Status == Previsto cuja DataPrevista seja exatamente <paramref name="data"/>.
    /// Usado pelo job de alertas de vencimento para os horizontes D-7, D-3 e D-0.
    /// </summary>
    public Task<IReadOnlyList<EventoCronograma>> ListPendentesVencendoEmAsync(LocalDate data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna os valores de todos os eventos com Status == Previsto.
    /// Usado pelo job de snapshot mensal para calcular total de parcelas abertas.
    /// </summary>
    public Task<IReadOnlyList<(decimal Valor, Sgcf.Domain.Common.Moeda Moeda)>> ListValoresPendentesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todos os eventos não liquidados (Previsto ou Atrasado) cujo ano de DataPrevista
    /// corresponda a <paramref name="ano"/> e cujo ContratoId esteja em <paramref name="contratoIds"/>.
    /// Usado pelo calendário de vencimentos do painel.
    /// </summary>
    public Task<IReadOnlyList<EventoCronograma>> ListAbertosParaAnoAsync(
        int ano,
        IReadOnlyCollection<Guid> contratoIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todos os eventos do tipo <c>Principal</c> para os contratos indicados,
    /// independentemente do ano ou status, ordenados por <c>ContratoId</c> e depois por
    /// <c>DataPrevista</c> ascendente. Usado para calcular o saldo devedor corrente
    /// na projeção de juros CDI do calendário de vencimentos.
    /// </summary>
    public Task<IReadOnlyList<EventoCronograma>> ListPrincipaisOrdenadosByContratoIdsAsync(
        IReadOnlyCollection<Guid> contratoIds,
        CancellationToken cancellationToken = default);
}
