using Sgcf.Domain.Tesouraria;

namespace Sgcf.Application.Tesouraria;

/// <summary>
/// Contrato de acesso a dados para <see cref="ContaBancaria"/>.
/// Implementado em <c>Sgcf.Infrastructure</c>; injetado via DI nos handlers de Application.
/// </summary>
public interface IContaBancariaRepository
{
    /// <summary>
    /// Retorna a conta pelo identificador, ou <c>null</c> se não encontrada
    /// (ou se já foi removida via soft delete).
    /// </summary>
    public Task<ContaBancaria?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Lista todas as contas do tenant corrente.
    /// Quando <paramref name="apenasAtivas"/> é <c>true</c>, filtra somente as ativas.
    /// Quando <c>null</c>, retorna todas (ativas e inativas).
    /// </summary>
    public Task<IReadOnlyList<ContaBancaria>> ListAsync(bool? apenasAtivas, CancellationToken ct);

    /// <summary>Adiciona a conta ao contexto do EF Core (pendente de <see cref="SaveChangesAsync"/>).</summary>
    public Task AddAsync(ContaBancaria conta, CancellationToken ct);

    /// <summary>Persiste todas as alterações pendentes no banco de dados.</summary>
    public Task SaveChangesAsync(CancellationToken ct);
}
