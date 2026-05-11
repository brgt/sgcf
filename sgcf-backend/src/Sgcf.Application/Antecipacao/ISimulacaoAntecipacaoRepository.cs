using Sgcf.Domain.Antecipacao;

namespace Sgcf.Application.Antecipacao;

public interface ISimulacaoAntecipacaoRepository
{
    public Task<SimulacaoAntecipacao?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    public Task<IReadOnlyList<SimulacaoAntecipacao>> ListByContratoAsync(Guid contratoId, CancellationToken cancellationToken);
    public Task AddAsync(SimulacaoAntecipacao simulacao, CancellationToken cancellationToken);
}
