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
    public void Add(Contrato contrato);
    public void AddFinimpDetail(FinimpDetail detail);
    public void AddLei4131Detail(Lei4131Detail detail);
    public void AddRefinimpDetail(RefinimpDetail detail);
    public void AddNceDetail(NceDetail detail);
    public void AddBalcaoCaixaDetail(BalcaoCaixaDetail detail);
    public void AddFgiDetail(FgiDetail detail);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    public Task<int> CountByAnoAsync(int ano, CancellationToken cancellationToken = default);
}
