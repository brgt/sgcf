using Microsoft.EntityFrameworkCore;
using Sgcf.Application.Antecipacao;
using Sgcf.Domain.Antecipacao;
using Sgcf.Infrastructure.Persistence;

namespace Sgcf.Infrastructure.Antecipacao;

internal sealed class SimulacaoAntecipacaoRepository(SgcfDbContext context) : ISimulacaoAntecipacaoRepository
{
    public Task<SimulacaoAntecipacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.SimulacoesAntecipacao.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SimulacaoAntecipacao>> ListByContratoAsync(
        Guid contratoId,
        CancellationToken cancellationToken)
    {
        List<SimulacaoAntecipacao> list = await context.SimulacoesAntecipacao
            .Where(s => s.ContratoId == contratoId)
            .OrderByDescending(s => s.DataSimulacao)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public async Task AddAsync(SimulacaoAntecipacao simulacao, CancellationToken cancellationToken)
    {
        context.SimulacoesAntecipacao.Add(simulacao);
        await context.SaveChangesAsync(cancellationToken);
    }
}
