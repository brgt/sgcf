using Sgcf.Domain.OrcamentosEncargo;

namespace Sgcf.Application.OrcamentosEncargo;

/// <summary>
/// Contrato de acesso a dados para <see cref="OrcamentoEncargo"/>.
/// Segue o padrão Unit of Work — a persistência ocorre via <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IOrcamentoEncargoRepository
{
    /// <summary>
    /// Busca o orçamento que corresponde exatamente à chave composta informada.
    /// Retorna <c>null</c> quando não existe registro para a combinação.
    /// </summary>
    public Task<OrcamentoEncargo?> GetAsync(
        int ano,
        int mes,
        string tipoEncargo,
        Guid? bancoId,
        Guid? contratoId,
        CancellationToken ct = default);

    /// <summary>
    /// Lista orçamentos dentro do intervalo de competência informado,
    /// com filtros opcionais por banco e tipo de encargo.
    /// </summary>
    public Task<IReadOnlyList<OrcamentoEncargo>> ListAsync(
        int deAno,
        int deMes,
        int ateAno,
        int ateMes,
        Guid? bancoId,
        string? tipoEncargo,
        CancellationToken ct = default);

    public void Add(OrcamentoEncargo orcamento);

    public Task<int> SaveChangesAsync(CancellationToken ct = default);
}
