using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class ContratoRepository(SgcfDbContext context) : IContratoRepository
{
    public Task<Contrato?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Contratos.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Contrato?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Contratos
            .Include(c => c.Parcelas)
            .Include(c => c.Garantias)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<FinimpDetail?> GetFinimpDetailAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<FinimpDetail>().FirstOrDefaultAsync(f => f.ContratoId == contratoId, cancellationToken);

    public Task<Lei4131Detail?> GetLei4131DetailAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<Lei4131Detail>().FirstOrDefaultAsync(l => l.ContratoId == contratoId, cancellationToken);

    public Task<RefinimpDetail?> GetRefinimpDetailAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<RefinimpDetail>().FirstOrDefaultAsync(r => r.ContratoId == contratoId, cancellationToken);

    public Task<NceDetail?> GetNceDetailAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<NceDetail>().FirstOrDefaultAsync(n => n.ContratoId == contratoId, cancellationToken);

    public Task<BalcaoCaixaDetail?> GetBalcaoCaixaDetailAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<BalcaoCaixaDetail>().FirstOrDefaultAsync(b => b.ContratoId == contratoId, cancellationToken);

    public Task<FgiDetail?> GetFgiDetailAsync(Guid contratoId, CancellationToken cancellationToken) =>
        context.Set<FgiDetail>().FirstOrDefaultAsync(f => f.ContratoId == contratoId, cancellationToken);

    /// <summary>
    /// Caminha pela cadeia de <c>ContratoPaiId</c> até encontrar o primeiro contrato
    /// cuja modalidade não seja <c>Refinimp</c> — o contrato original.
    /// A busca começa a partir do contrato com id <paramref name="contratoId"/>.
    /// </summary>
    public async Task<Contrato?> GetAncestraNaoRefinimpAsync(Guid contratoId, CancellationToken cancellationToken)
    {
        Guid? idAtual = contratoId;
        while (idAtual.HasValue)
        {
            Contrato? atual = await context.Contratos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == idAtual.Value, cancellationToken);

            if (atual is null)
            {
                return null;
            }

            if (atual.Modalidade != ModalidadeContrato.Refinimp)
            {
                return atual;
            }

            idAtual = atual.ContratoPaiId;
        }

        return null;
    }

    public async Task<IReadOnlyList<Contrato>> ListAsync(CancellationToken cancellationToken)
    {
        List<Contrato> list = await context.Contratos.ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public void Add(Contrato contrato) => context.Contratos.Add(contrato);

    public void AddFinimpDetail(FinimpDetail detail) => context.Set<FinimpDetail>().Add(detail);

    public void AddLei4131Detail(Lei4131Detail detail) => context.Set<Lei4131Detail>().Add(detail);

    public void AddRefinimpDetail(RefinimpDetail detail) => context.Set<RefinimpDetail>().Add(detail);

    public void AddNceDetail(NceDetail detail) => context.Set<NceDetail>().Add(detail);

    public void AddBalcaoCaixaDetail(BalcaoCaixaDetail detail) => context.Set<BalcaoCaixaDetail>().Add(detail);

    public void AddFgiDetail(FgiDetail detail) => context.Set<FgiDetail>().Add(detail);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

    public Task<int> CountByAnoAsync(int ano, CancellationToken cancellationToken) =>
        context.Contratos
            .IgnoreQueryFilters()
            .CountAsync(c => c.DataContratacao.Year == ano, cancellationToken);
}
