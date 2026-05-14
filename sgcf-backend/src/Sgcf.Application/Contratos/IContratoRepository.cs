using Sgcf.Application.Contratos.Queries;
using Sgcf.Domain.Contratos;

namespace Sgcf.Application.Contratos;

public interface IContratoRepository
{
    public Task<Contrato?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<Contrato?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    public Task<FinimpDetail?> GetFinimpDetailAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<Lei4131Detail?> GetLei4131DetailAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<RefinimpDetail?> GetRefinimpDetailAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<NceDetail?> GetNceDetailAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<BalcaoCaixaDetail?> GetBalcaoCaixaDetailAsync(Guid contratoId, CancellationToken cancellationToken = default);
    public Task<FgiDetail?> GetFgiDetailAsync(Guid contratoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Caminha para cima pela cadeia de <c>ContratoPaiId</c> até encontrar o contrato
    /// que não seja modalidade REFINIMP — o ancestral original (contrato raiz).
    /// Retorna <c>null</c> se o contrato inicial não for encontrado.
    /// </summary>
    public Task<Contrato?> GetAncestraNaoRefinimpAsync(Guid contratoId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Contrato>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna uma página de contratos que satisfazem o filtro, junto com o total de registros
    /// que satisfazem o filtro (sem paginação) para cálculo do número de páginas no cliente.
    /// </summary>
    public Task<(IReadOnlyList<Contrato> Items, int Total)> ListPagedAsync(
        ContratoFilter filter,
        string sort,
        string dir,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    public void Add(Contrato contrato);
    public void AddFinimpDetail(FinimpDetail detail);
    public void AddLei4131Detail(Lei4131Detail detail);
    public void AddRefinimpDetail(RefinimpDetail detail);
    public void AddNceDetail(NceDetail detail);
    public void AddBalcaoCaixaDetail(BalcaoCaixaDetail detail);
    public void AddFgiDetail(FgiDetail detail);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    public Task<int> CountByAnoAsync(int ano, CancellationToken cancellationToken = default);

    /// <summary>Conta contratos com Status == Ativo (não deletados).</summary>
    public Task<int> CountAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna id, valor principal decimal e moeda de todos os contratos ativos.
    /// Usado pelo job de snapshot mensal para converter e somar em BRL.
    /// </summary>
    public Task<IReadOnlyList<(Guid Id, decimal ValorPrincipal, Sgcf.Domain.Common.Moeda Moeda)>> ListAtivosValoresPrincipaisAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna a soma dos ValorPrincipal de contratos ativos vinculados ao banco indicado, em decimal.
    /// BRL direto — não faz conversão FX. Usado pelo job de alerta de exposição de banco.
    /// </summary>
    public Task<decimal> GetSaldoPrincipalTotalPorBancoAsync(Guid bancoId, CancellationToken cancellationToken = default);

    /// <summary>Retorna todos os contratos com Status == Ativo para cálculo de provisão diária de juros.</summary>
    public Task<IReadOnlyList<Contrato>> ListAtivosComTaxaAsync(CancellationToken cancellationToken = default);
}
