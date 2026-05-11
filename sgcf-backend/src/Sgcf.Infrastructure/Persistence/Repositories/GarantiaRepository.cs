using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Contratos;
using Sgcf.Domain.Contratos;

namespace Sgcf.Infrastructure.Persistence.Repositories;

internal sealed class GarantiaRepository(SgcfDbContext context) : IGarantiaRepository
{
    public async Task<IReadOnlyList<Garantia>> ListByContratoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        List<Garantia> list = await context.Garantias
            .Where(g => g.ContratoId == contratoId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public Task<Garantia?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Garantias.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public void Add(Garantia garantia) => context.Garantias.Add(garantia);

    public void AddCdbCativoDetail(GarantiaCdbCativoDetail detail) =>
        context.Set<GarantiaCdbCativoDetail>().Add(detail);

    public void AddSblcDetail(GarantiaSblcDetail detail) =>
        context.Set<GarantiaSblcDetail>().Add(detail);

    public void AddAvalDetail(GarantiaAvalDetail detail) =>
        context.Set<GarantiaAvalDetail>().Add(detail);

    public void AddAlienacaoFiduciariaDetail(GarantiaAlienacaoFiduciariaDetail detail) =>
        context.Set<GarantiaAlienacaoFiduciariaDetail>().Add(detail);

    public void AddDuplicatasDetail(GarantiaDuplicatasDetail detail) =>
        context.Set<GarantiaDuplicatasDetail>().Add(detail);

    public void AddRecebiveisCartaoDetail(GarantiaRecebiveisCartaoDetail detail) =>
        context.Set<GarantiaRecebiveisCartaoDetail>().Add(detail);

    public void AddBoletoBancarioDetail(GarantiaBoletoBancarioDetail detail) =>
        context.Set<GarantiaBoletoBancarioDetail>().Add(detail);

    public void AddFgiDetail(GarantiaFgiDetail detail) =>
        context.Set<GarantiaFgiDetail>().Add(detail);

    /// <summary>
    /// Soma os percentuais de faturamento comprometido (como fração) de todas as garantias
    /// ativas do tipo RecebiveisCartao para o contrato informado.
    /// </summary>
    public async Task<decimal> GetTotalPercentualFaturamentoCartaoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        // We need to join Garantia (for contratoId and status filter) with the detail table.
        // Using a subquery via LINQ to keep it readable and correct.
        List<Guid> garantiaIdsAtivas = await context.Garantias
            .Where(g => g.ContratoId == contratoId
                && g.Tipo == TipoGarantia.RecebiveisCartao
                && g.Status == StatusGarantia.Ativa)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        if (garantiaIdsAtivas.Count == 0)
        {
            return 0m;
        }

        decimal total = await context.Set<GarantiaRecebiveisCartaoDetail>()
            .Where(d => garantiaIdsAtivas.Contains(d.GarantiaId))
            .SumAsync(d => d.PercentualFaturamentoComprometidoDecimal, cancellationToken);

        return total;
    }

    /// <summary>
    /// Retorna detalhes de CDB cativo para a garantia especificada cujo vencimento é até a data limite.
    /// A data limite é passada pelo handler (calculada usando IClock), evitando injeção de IClock no repositório.
    /// </summary>
    public async Task<IReadOnlyList<GarantiaCdbCativoDetail>> ListCdbAtivosComVencimentoAteAsync(
        Guid garantiaId,
        LocalDate dataLimite,
        CancellationToken cancellationToken)
    {
        List<GarantiaCdbCativoDetail> lista = await context.GarantiaCdbCativoDetails
            .Where(d => d.GarantiaId == garantiaId && d.DataVencimentoCdb <= dataLimite)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return lista.AsReadOnly();
    }

    /// <summary>
    /// Retorna detalhes de boleto bancário para a garantia especificada cujo vencimento final é até a data limite.
    /// </summary>
    public async Task<IReadOnlyList<GarantiaBoletoBancarioDetail>> ListBoletosAtivosComVencimentoAteAsync(
        Guid garantiaId,
        LocalDate dataLimite,
        CancellationToken cancellationToken)
    {
        List<GarantiaBoletoBancarioDetail> lista = await context.GarantiaBoletoBancarioDetails
            .Where(d => d.GarantiaId == garantiaId && d.DataVencimentoFinal <= dataLimite)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return lista.AsReadOnly();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
