using Sgcf.Domain.Contabilidade;

namespace Sgcf.Application.Contabilidade;

/// <summary>
/// Contrato de acesso a dados para <see cref="DadosContabeisMensal"/>.
/// Segue o padrão Unit of Work — a persistência ocorre via <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IDadosContabeisRepository
{
    /// <summary>
    /// Busca o registro de dados contábeis para o ano/mês informado.
    /// Retorna <c>null</c> quando não existe registro para a competência.
    /// </summary>
    public Task<DadosContabeisMensal?> GetByCompetenciaAsync(int ano, int mes, CancellationToken ct = default);

    /// <summary>
    /// Lista todos os registros dentro dos últimos 12 meses contados a partir de
    /// <paramref name="anoReferencia"/>/<paramref name="mesReferencia"/> (inclusive).
    /// O resultado pode ter menos de 12 entradas quando dados estão ausentes.
    /// </summary>
    public Task<IReadOnlyList<DadosContabeisMensal>> ListUltimos12MesesAsync(
        int anoReferencia,
        int mesReferencia,
        CancellationToken ct = default);

    public void Add(DadosContabeisMensal dados);

    public void Update(DadosContabeisMensal dados);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
