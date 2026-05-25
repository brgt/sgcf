using Microsoft.EntityFrameworkCore;
using NodaTime;
using Sgcf.Application.Cotacoes;
using Sgcf.Domain.Contratos;
using Sgcf.Domain.Cotacoes;

namespace Sgcf.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação de <see cref="ILimiteBancoRepository"/> usando EF Core + PostgreSQL.
/// </summary>
internal sealed class LimiteBancoRepository(SgcfDbContext context) : ILimiteBancoRepository
{
    public void Add(LimiteBanco limite) => context.LimitesBanco.Add(limite);

    public void Update(LimiteBanco limite) => context.LimitesBanco.Update(limite);

    public Task<LimiteBanco?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .Include(l => l.Historico)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <inheritdoc/>
    public Task<LimiteBanco?> GetByIdTrackingAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .Include(l => l.Historico)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <summary>
    /// Retorna o limite vigente (sem data_vigencia_fim) para a combinação banco+modalidade.
    /// "Vigente" é definido como DataVigenciaFim == null (sem encerramento programado).
    /// RevisoesGarantiasExigidas é carregado eagerly para que o pré-preenchimento automático
    /// de garantia funcione sem lazy-loading.
    /// </summary>
    public Task<LimiteBanco?> GetByBancoModalidadeAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        CancellationToken cancellationToken = default) =>
        context.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.BancoId == bancoId
                  && l.Modalidade == modalidade
                  && l.DataVigenciaFim == null,
                cancellationToken);

    /// <inheritdoc/>
    public Task<LimiteBanco?> GetVigenteByBancoModalidadeAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        LocalDate dataReferencia,
        CancellationToken cancellationToken = default) =>
        // Critério: DataVigenciaInicio <= dataReferencia
        //       AND (DataVigenciaFim IS NULL OR DataVigenciaFim >= dataReferencia)
        // Eager-load de RevisoesGarantiasExigidas com Itens: necessário para SC-03 (RevisaoGarantiasVigente).
        // AsNoTracking: leitura — o handler não persiste o LimiteBanco aqui (apenas lê o Id e a revisão vigente).
        context.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.BancoId == bancoId
                  && l.Modalidade == modalidade
                  && l.DataVigenciaInicio <= dataReferencia
                  && (l.DataVigenciaFim == null || l.DataVigenciaFim.Value >= dataReferencia),
                cancellationToken);

    /// <summary>
    /// Retorna o primeiro limite que se sobrepõe ao período [inicio, fim] para o par bancoId+modalidade.
    /// Overlap: existente.Inicio &lt;= fim (ou fim é null) AND inicio &lt;= existente.Fim (ou existente.Fim é null).
    /// </summary>
    public Task<LimiteBanco?> FindOverlappingAsync(
        Guid bancoId,
        ModalidadeContrato modalidade,
        LocalDate inicio,
        LocalDate? fim,
        Guid? excluirId = null,
        CancellationToken cancellationToken = default) =>
        context.LimitesBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.BancoId == bancoId
                  && l.Modalidade == modalidade
                  && (excluirId == null || l.Id != excluirId.Value)
                  // l.Inicio <= fim (null fim = +∞, always true)
                  && (fim == null || l.DataVigenciaInicio <= fim.Value)
                  // inicio <= l.Fim (null l.Fim = +∞, always true)
                  && (l.DataVigenciaFim == null || inicio <= l.DataVigenciaFim.Value),
                cancellationToken);

    public async Task<IReadOnlyList<LimiteBanco>> ListAsync(
        Guid? bancoId,
        ModalidadeContrato? modalidade,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LimiteBanco> q = context.LimitesBanco
            .Include(l => l.RevisoesGarantiasExigidas)
                .ThenInclude(r => r.Itens)
            .Include(l => l.Historico)
            .AsNoTracking();

        if (bancoId.HasValue)
        {
            q = q.Where(l => l.BancoId == bancoId.Value);
        }

        if (modalidade.HasValue)
        {
            q = q.Where(l => l.Modalidade == modalidade.Value);
        }

        List<LimiteBanco> list = await q
            .OrderBy(l => l.BancoId)
            .ThenBy(l => l.Modalidade)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GarantiaExigidaRevisao>> GetRevisoesGarantiasAsync(
        Guid limiteBancoId,
        CancellationToken cancellationToken = default)
    {
        List<GarantiaExigidaRevisao> revisoes = await context
            .GarantiasExigidasRevisoes
            .Include(r => r.Itens)
            .AsNoTracking()
            .Where(r => r.LimiteBancoId == limiteBancoId)
            .OrderBy(r => r.VigenciaInicio)
            .ToListAsync(cancellationToken);

        return revisoes.AsReadOnly();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
