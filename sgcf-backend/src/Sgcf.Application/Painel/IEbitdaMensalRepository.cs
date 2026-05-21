using Sgcf.Domain.Painel;

namespace Sgcf.Application.Painel;

/// <summary>
/// Contrato de acesso a dados para <see cref="EbitdaMensal"/>.
/// Usa o padrão de Unit of Work compartilhado — a persistência ocorre via <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IEbitdaMensalRepository
{
    /// <summary>Busca o registro de EBITDA para o ano/mês especificado. Retorna null se não existir.</summary>
    public Task<EbitdaMensal?> GetAsync(int ano, int mes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todos os registros de EBITDA dentro dos últimos 12 meses contados a partir de
    /// <paramref name="anoReferencia"/>/<paramref name="mesReferencia"/> (mês inclusive).
    /// O resultado pode ter menos de 12 entradas quando dados estão ausentes.
    /// </summary>
    public Task<IReadOnlyList<EbitdaMensal>> ListUltimos12MesesAsync(
        int anoReferencia,
        int mesReferencia,
        CancellationToken cancellationToken = default);

    public void Add(EbitdaMensal ebitda);

    public void Update(EbitdaMensal ebitda);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
