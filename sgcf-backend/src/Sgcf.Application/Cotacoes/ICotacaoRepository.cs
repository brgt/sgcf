using Sgcf.Domain.Cotacoes;

namespace Sgcf.Application.Cotacoes;

/// <summary>
/// Porta de persistência para o agregado <see cref="Cotacao"/>.
/// Implementação pertence a Sgcf.Infrastructure.
/// </summary>
public interface ICotacaoRepository
{
    public void Add(Cotacao cotacao);
    public void Update(Cotacao cotacao);
    public void Remove(Cotacao cotacao);

    public Task<Cotacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Carrega a cotação com todas as propostas. Necessário para operações de escrita no agregado.</summary>
    public Task<Cotacao?> GetByIdWithPropostasAsync(Guid id, CancellationToken cancellationToken = default);

    public Task<(IReadOnlyList<Cotacao> Items, int Total)> ListPagedAsync(
        CotacaoFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera o próximo código interno sequencial para o ano dado.
    /// Formato: COT-{ano}-{seq:05d} (ex: COT-2026-00001). SPEC §13 decisão 4.
    /// </summary>
    public Task<string> GerarProximoCodigoInternoAsync(int ano, CancellationToken cancellationToken = default);
}
