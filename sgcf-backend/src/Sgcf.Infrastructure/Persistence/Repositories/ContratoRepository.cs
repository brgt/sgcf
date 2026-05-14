using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Contratos;
using Sgcf.Application.Contratos.Queries;
using Sgcf.Domain.Alertas;
using Sgcf.Domain.Common;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Hedge;

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
        List<Contrato> list = await context.Contratos
            .Include(c => c.Parcelas)
            .Include(c => c.Garantias)
            .ToListAsync(cancellationToken);
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

    public Task<int> CountAtivosAsync(CancellationToken cancellationToken) =>
        context.Contratos.CountAsync(c => c.Status == StatusContrato.Ativo, cancellationToken);

    public async Task<IReadOnlyList<(Guid Id, decimal ValorPrincipal, Moeda Moeda)>> ListAtivosValoresPrincipaisAsync(CancellationToken cancellationToken)
    {
        List<(Guid, decimal, Moeda)> list = await context.Contratos
            .Where(c => c.Status == StatusContrato.Ativo)
            .Select(c => new ValueTuple<Guid, decimal, Moeda>(c.Id, c.ValorPrincipalDecimal, c.Moeda))
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public Task<decimal> GetSaldoPrincipalTotalPorBancoAsync(Guid bancoId, CancellationToken cancellationToken) =>
        context.Contratos
            .Where(c => c.BancoId == bancoId && c.Status == StatusContrato.Ativo)
            .SumAsync(c => c.ValorPrincipalDecimal, cancellationToken);

    public async Task<IReadOnlyList<Contrato>> ListAtivosComTaxaAsync(CancellationToken cancellationToken)
    {
        List<Contrato> list = await context.Contratos
            .Where(c => c.Status == StatusContrato.Ativo)
            .ToListAsync(cancellationToken);
        return list.AsReadOnly();
    }

    public async Task<(IReadOnlyList<Contrato> Items, int Total)> ListPagedAsync(
        ContratoFilter filter, string sort, string dir, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Contrato> q = context.Contratos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string pattern = $"%{filter.Search.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.NumeroExterno, pattern)
                          || (c.CodigoInterno != null && EF.Functions.ILike(c.CodigoInterno, pattern)));
        }

        if (filter.BancoId.HasValue)
        {
            q = q.Where(c => c.BancoId == filter.BancoId.Value);
        }

        if (filter.Modalidade.HasValue)
        {
            q = q.Where(c => c.Modalidade == filter.Modalidade.Value);
        }

        if (filter.Moeda.HasValue)
        {
            q = q.Where(c => c.Moeda == filter.Moeda.Value);
        }

        if (filter.Status.HasValue)
        {
            q = q.Where(c => c.Status == filter.Status.Value);
        }

        if (filter.DataVencimentoDe.HasValue)
        {
            q = q.Where(c => c.DataVencimento >= filter.DataVencimentoDe.Value);
        }

        if (filter.DataVencimentoAte.HasValue)
        {
            q = q.Where(c => c.DataVencimento <= filter.DataVencimentoAte.Value);
        }

        if (filter.ValorPrincipalMin.HasValue)
        {
            q = q.Where(c => c.ValorPrincipalDecimal >= filter.ValorPrincipalMin.Value);
        }

        if (filter.ValorPrincipalMax.HasValue)
        {
            q = q.Where(c => c.ValorPrincipalDecimal <= filter.ValorPrincipalMax.Value);
        }

        if (filter.TemHedge.HasValue)
        {
            bool temHedge = filter.TemHedge.Value;
            q = q.Where(c => context.InstrumentosHedge.Any(h => h.ContratoId == c.Id) == temHedge);
        }

        if (filter.TemGarantia.HasValue)
        {
            bool temGarantia = filter.TemGarantia.Value;
            q = q.Where(c => context.Garantias.Any(g => g.ContratoId == c.Id) == temGarantia);
        }

        if (filter.TemAlertaVencimento.HasValue)
        {
            bool temAlerta = filter.TemAlertaVencimento.Value;
            q = q.Where(c => context.AlertasVencimento.Any(a => a.ContratoId == c.Id) == temAlerta);
        }

        // Apply sort — whitelist is enforced by the handler before calling this method
        bool ascending = string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
        q = sort switch
        {
            "DataContratacao" => ascending
                ? q.OrderBy(c => c.DataContratacao)
                : q.OrderByDescending(c => c.DataContratacao),
            "ValorPrincipal" => ascending
                ? q.OrderBy(c => c.ValorPrincipalDecimal)
                : q.OrderByDescending(c => c.ValorPrincipalDecimal),
            "NumeroExterno" => ascending
                ? q.OrderBy(c => c.NumeroExterno)
                : q.OrderByDescending(c => c.NumeroExterno),
            _ => ascending
                ? q.OrderBy(c => c.DataVencimento)
                : q.OrderByDescending(c => c.DataVencimento),
        };

        int total = await q.CountAsync(cancellationToken);
        List<Contrato> items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items.AsReadOnly(), total);
    }
}
